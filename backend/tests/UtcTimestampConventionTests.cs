using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NextStep.Shared.Persistence;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// The application stores UTC in "timestamp without time zone" columns. That mapping used to come
/// from a process-wide <c>AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)</c>,
/// which was a crutch: the switch is global to the process, so it also switched off Npgsql's
/// strict UTC validation for every "timestamp with time zone" column, including Hangfire's job
/// storage, and it silently accepted a local time where UTC was expected.
/// The mapping is now declared by <see cref="ModuleDbContext"/>, and these tests fail if the
/// process-wide switch comes back or if a DateTime property escapes the convention.
/// </summary>
public class UtcTimestampConventionTests
{
    [Fact]
    public void Npgsql_legacy_timestamp_behaviour_is_not_enabled()
    {
        var isSet = AppContext.TryGetSwitch("Npgsql.EnableLegacyTimestampBehavior", out var enabled);

        isSet.Should().BeFalse(
            "the switch is process-wide and disables Npgsql's strict UTC validation, " +
            "so a local DateTime would be stored as if it were UTC without any error");
        enabled.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ModuleBoundaryTests.ModuleContexts), MemberType = typeof(ModuleBoundaryTests))]
    public void Every_date_time_property_is_stored_as_utc_without_time_zone(Type contextType, string module, string schema)
    {
        using var context = CreateContext(contextType);

        var properties = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
            .ToList();

        properties.Should().NotBeEmpty($"{module} stores timestamps");

        var wrong = properties
            .Where(property => property.GetColumnType() != "timestamp without time zone")
            .Select(property => $"{schema}.{property.DeclaringType.GetTableName()}.{property.GetColumnName()}")
            .ToList();

        wrong.Should().BeEmpty(
            $"{module} columns hold naive UTC by convention; a different column type would either " +
            "shift the stored instant or re-enable the conversion the convention exists to avoid");
    }

    /// <summary>
    /// A local DateTime is what made the bug class invisible: the writers only ever produce UTC, so
    /// a regression would show up as a value one hour off, not as an exception. Asserting the
    /// declared type keeps the convention and the switch from drifting apart.
    /// </summary>
    [Fact]
    public void Convention_is_declared_by_the_shared_context_rather_than_by_a_global_switch()
    {
        typeof(ModuleDbContext).GetMethod(
            "ConfigureConventions",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Should().NotBeNull("ModuleDbContext owns the timestamp convention for every module");
    }

    private static ModuleDbContext CreateContext(Type contextType)
    {
        var optionsType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsType)!;
        builder.UseNpgsql("Host=localhost;Database=design_time_only");
        return (ModuleDbContext)Activator.CreateInstance(contextType, builder.Options)!;
    }
}
