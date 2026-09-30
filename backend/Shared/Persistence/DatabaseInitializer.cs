using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NextStep.Shared.Persistence;

/// <summary>What one module schema is missing for the running build to be safe to serve traffic on.</summary>
internal sealed record SchemaReadiness(
    string Schema,
    IReadOnlyList<string> PendingMigrations,
    IReadOnlyList<string> MissingColumns);

/// <summary>
/// Schema ownership at startup, split by who is allowed to write.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="InitializeDatabaseAsync"/> is the only path that writes: legacy upgrade, baseline,
/// migrations and seeding. It belongs to a dedicated step, because several web containers booting
/// at the same time would run those DDL statements concurrently and fight over the same locks.
/// </para>
/// <para>
/// <see cref="VerifySchemaIsUpToDateAsync"/> is what a web container is allowed to do: read the
/// schema and refuse to serve traffic if it does not match the build. It never writes, so starting
/// N replicas stays a read-only operation and cannot deadlock, however many start at once.
/// </para>
/// </remarks>
public static class DatabaseInitializer
{
    /// <summary>
    /// Write path: upgrades a legacy layout, applies every module's migrations and seeds reference
    /// data. Run it from the dedicated migration step (<c>dotnet NextStep.dll --migrate</c>), never
    /// from a web container that runs more than one replica.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer).FullName!);
        var contexts = services.GetServices<ModuleDbContext>().ToList();

        await LegacySchemaUpgrader.UpgradeAsync(contexts, logger, ct);

        var drifted = new List<string>();

        foreach (var context in contexts)
        {
            await BaselineExistingSchemaAsync(context, logger, ct);
            await context.Database.MigrateAsync(ct);
            logger.LogInformation("Schema {Schema} is up to date", context.Schema);

            var missing = await FindMissingColumnsAsync(context, ct);
            if (missing.Count > 0)
                drifted.Add($"{context.Schema}: {string.Join(", ", missing)}");
        }

        // Refuse to start rather than serve 500s on the first request that touches the gap.
        if (drifted.Count > 0)
            throw new SchemaMismatchException(
                "The database does not match the EF model, so the application cannot start." +
                Environment.NewLine +
                "Missing columns: " + string.Join(Environment.NewLine, drifted) + Environment.NewLine +
                "Migrations are marked as applied but do not match the tables they should have created." +
                "Either rebuild the database from the migrations (discards data) or add the missing DDL.");

        foreach (var seeder in services.GetServices<IModuleSeeder>())
            await seeder.SeedAsync(ct);
    }

    /// <summary>
    /// Read-only gate for a web container: refuses to serve traffic when the database is behind or
    /// has drifted from the model. It issues no DDL and takes no write lock, so any number of
    /// replicas can run it at the same time.
    /// </summary>
    public static async Task VerifySchemaIsUpToDateAsync(this WebApplication app, CancellationToken ct = default)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer).FullName!);
        var contexts = services.GetServices<ModuleDbContext>().ToList();

        var findings = new List<SchemaReadiness>();

        foreach (var context in contexts)
        {
            var pending = await PendingMigrationsAsync(context, ct);
            var missing = await FindMissingColumnsAsync(context, ct);

            findings.Add(new SchemaReadiness(context.Schema, pending, missing));
        }

        EnsureSchemaIsReady(findings);

        logger.LogInformation("Schema check passed for {SchemaCount} module schemas", contexts.Count);
    }

    /// <summary>
    /// Decides whether a set of findings may serve traffic. Kept apart from the database calls so
    /// the rule itself can be tested without a server.
    /// </summary>
    internal static void EnsureSchemaIsReady(IReadOnlyList<SchemaReadiness> findings)
    {
        var behind = findings.Where(f => f.PendingMigrations.Count > 0).ToList();
        var drifted = findings.Where(f => f.MissingColumns.Count > 0).ToList();

        if (behind.Count == 0 && drifted.Count == 0)
            return;

        var message = new StringBuilder(
            "The database does not match the version this build expects, so the application cannot start.")
            .Append(Environment.NewLine);

        if (behind.Count > 0)
        {
            message.Append("Migrations that are not applied:").Append(Environment.NewLine);
            message.Append(string.Join(Environment.NewLine, behind.SelectMany(f =>
                f.PendingMigrations.Select(m => $"  {f.Schema}: {m}"))));
            message.Append(Environment.NewLine);
        }

        if (drifted.Count > 0)
        {
            message.Append("Missing columns:").Append(Environment.NewLine);
            message.Append(string.Join(Environment.NewLine, drifted.Select(f =>
                $"  {f.Schema}: {string.Join(", ", f.MissingColumns)}")));
            message.Append(Environment.NewLine);
        }

        message.Append("A web container never changes the schema. Run the migration step before deploying it:")
            .Append(Environment.NewLine)
            .Append("  dotnet NextStep.dll --migrate");

        throw new SchemaMismatchException(message.ToString());
    }

    /// <summary>
    /// Migrations this build would still apply. A schema with no history table has none of them
    /// applied, which is exactly what a never-migrated database looks like.
    /// </summary>
    private static async Task<IReadOnlyList<string>> PendingMigrationsAsync(ModuleDbContext context, CancellationToken ct)
    {
        var existing = await ExistingTablesAsync(context, ct);
        if (!existing.Contains(ModuleDbContext.MigrationsHistoryTable))
            return context.Database.GetMigrations().ToList();

        return (await context.Database.GetPendingMigrationsAsync(ct)).ToList();
    }

    /// <summary>
    /// A schema whose tables already exist but that has no migrations history (upgraded legacy
    /// database) gets its initial migration marked as applied instead of re-creating the tables.
    /// </summary>
    /// <remarks>
    /// Adopting tables this way asserts that the existing schema is exactly what the initial
    /// migration would have produced. That assertion is only safe when it is actually true, so the
    /// columns are compared first: a database older than the current <c>InitialSchema</c> is
    /// missing whatever that migration later added, and marking it applied would hide those
    /// columns permanently. Detecting that here turns silent drift into a startup failure.
    /// </remarks>
    private static async Task BaselineExistingSchemaAsync(ModuleDbContext context, ILogger logger, CancellationToken ct)
    {
        var existing = await ExistingTablesAsync(context, ct);
        if (existing.Contains(ModuleDbContext.MigrationsHistoryTable))
            return;

        if (!TablesOf(context).Any(existing.Contains))
            return;

        var initialMigration = context.Database.GetMigrations().First();

        var missing = await FindMissingColumnsAsync(context, ct);
        if (missing.Count > 0)
            throw new SchemaMismatchException(
                $"Schema {context.Schema} has no migration history and cannot adopt its existing " +
                $"tables: {string.Join(", ", missing)} {Environment.NewLine}" +
                $"Those tables are older than migration {initialMigration}, which is why the columns " +
                "are absent. Marking that migration as applied would leave the database permanently " +
                "incomplete." + Environment.NewLine +
                "Rebuild the database from the migrations (discards data) or add the missing DDL.");

        var history = context.GetService<IHistoryRepository>();

        await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        await context.Database.ExecuteSqlRawAsync(
            history.GetInsertScript(new HistoryRow(initialMigration, ProductInfo.GetVersion())), ct);

        logger.LogWarning(
            "Schema {Schema}: existing tables adopted, migration {Migration} marked as applied",
            context.Schema, initialMigration);
    }

    /// <summary>Columns the EF model maps that the database does not have (schema drift).</summary>
    private static async Task<IReadOnlyList<string>> FindMissingColumnsAsync(ModuleDbContext context, CancellationToken ct)
    {
        var actual = new HashSet<string>(StringComparer.Ordinal);
        await ReadAsync(context,
            "SELECT table_name || '.' || column_name FROM information_schema.columns WHERE table_schema = @schema",
            reader => actual.Add(reader.GetString(0)), ct);

        var missing = new List<string>();
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue;

            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            missing.AddRange(entity.GetProperties()
                .Select(p => p.GetColumnName(store))
                .Where(c => c is not null && !actual.Contains($"{table}.{c}"))
                .Select(c => $"{table}.{c}"));
        }

        return missing;
    }

    internal static IReadOnlyList<string> TablesOf(DbContext context) =>
        context.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .OfType<string>()
            .Distinct()
            .ToList();

    private static async Task<HashSet<string>> ExistingTablesAsync(ModuleDbContext context, CancellationToken ct)
    {
        var tables = new HashSet<string>(StringComparer.Ordinal);
        await ReadAsync(context,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema",
            reader => tables.Add(reader.GetString(0)), ct);
        return tables;
    }

    private static async Task ReadAsync(ModuleDbContext context, string sql, Action<DbDataReader> onRow, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "schema";
            parameter.Value = context.Schema;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                onRow(reader);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }
}
