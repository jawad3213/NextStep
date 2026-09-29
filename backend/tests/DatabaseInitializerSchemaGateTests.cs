using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NextStep.Shared.Persistence;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// A web container used to apply the migrations itself, so several replicas booting together ran
/// the same DDL at the same time and fought over locks. The web container now only reads the
/// schema and refuses to serve traffic, and these tests pin the rule that decides.
/// </summary>
public class DatabaseInitializerSchemaGateTests
{
    private static SchemaReadiness Clean(string schema = "applications") =>
        new(schema, Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public void Matching_Schema_May_Start()
    {
        var act = () => DatabaseInitializer.EnsureSchemaIsReady(new[]
        {
            Clean("applications"),
            Clean("messaging"),
            Clean("profile")
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Pending_Migration_Blocks_Start_And_Names_The_Step()
    {
        var findings = new[]
        {
            Clean("applications"),
            new SchemaReadiness("messaging", new[] { "20260929120000_AddAttachment" }, Array.Empty<string>())
        };

        var act = () => DatabaseInitializer.EnsureSchemaIsReady(findings);

        act.Should().Throw<SchemaMismatchException>()
            .Which.Message.Should().ContainAll("messaging", "20260929120000_AddAttachment", "--migrate");
    }

    [Fact]
    public void Missing_Column_Blocks_Start()
    {
        var findings = new[]
        {
            new SchemaReadiness("profile", Array.Empty<string>(), new[] { "profile_data.language" })
        };

        var act = () => DatabaseInitializer.EnsureSchemaIsReady(findings);

        act.Should().Throw<SchemaMismatchException>()
            .Which.Message.Should().ContainAll("profile", "profile_data.language");
    }

    [Fact]
    public void Both_Problems_Are_Reported_Together()
    {
        // Reporting only the first one would send an operator through a migration run and straight
        // back into the same failure.
        var findings = new[]
        {
            new SchemaReadiness("applications", new[] { "InitialSchema" }, Array.Empty<string>()),
            new SchemaReadiness("messaging", Array.Empty<string>(), new[] { "email_draft.subject" })
        };

        var act = () => DatabaseInitializer.EnsureSchemaIsReady(findings);

        act.Should().Throw<SchemaMismatchException>()
            .Which.Message.Should().ContainAll("InitialSchema", "email_draft.subject");
    }

    [Fact]
    public void A_Never_Migrated_Schema_Is_Reported_As_Behind_Not_As_Drift()
    {
        // Every column is missing on a fresh database, which alone would read as corruption. The
        // pending migrations are the real explanation and must be the first thing an operator sees.
        var findings = new[]
        {
            new SchemaReadiness(
                "applications",
                new[] { "InitialSchema" },
                Enumerable.Range(0, 40).Select(i => $"offres_emploi.column_{i}").ToList())
        };

        var act = () => DatabaseInitializer.EnsureSchemaIsReady(findings);

        act.Should().Throw<SchemaMismatchException>()
            .Which.Message.Should().Contain("Migrations that are not applied")
            .And.Contain("--migrate");
    }

    [Fact]
    public void The_Failure_Never_Pretends_The_Container_Can_Fix_It_Itself()
    {
        var findings = new[] { new SchemaReadiness("sourcing", new[] { "InitialSchema" }, Array.Empty<string>()) };

        var act = () => DatabaseInitializer.EnsureSchemaIsReady(findings);

        // The old behaviour applied the missing migration at boot. The message has to point at the
        // separate step, or an operator will read the failure as a reason to start a web task by hand.
        act.Should().Throw<SchemaMismatchException>()
            .Which.Message.Should().Contain("A web container never changes the schema");
    }
}
