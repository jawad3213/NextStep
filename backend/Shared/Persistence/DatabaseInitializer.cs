using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NextStep.Shared.Persistence;

/// <summary>
/// Single schema system at startup: every module applies its own EF migrations in its own
/// schema, then seeds its reference data. Databases created before the module split
/// (all tables in "public") are upgraded once by <see cref="LegacySchemaUpgrader"/>.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer).FullName!);
        var contexts = services.GetServices<ModuleDbContext>().ToList();

        await LegacySchemaUpgrader.UpgradeAsync(contexts, logger, ct);

        foreach (var context in contexts)
        {
            await BaselineExistingSchemaAsync(context, logger, ct);
            await context.Database.MigrateAsync(ct);
            logger.LogInformation("Schema {Schema} is up to date", context.Schema);
        }

        foreach (var context in contexts)
            await ReportMissingColumnsAsync(context, logger, ct);

        foreach (var seeder in services.GetServices<IModuleSeeder>())
            await seeder.SeedAsync(ct);
    }

    /// <summary>
    /// A schema whose tables already exist but that has no migrations history (upgraded legacy
    /// database) gets its initial migration marked as applied instead of re-creating the tables.
    /// </summary>
    private static async Task BaselineExistingSchemaAsync(ModuleDbContext context, ILogger logger, CancellationToken ct)
    {
        var existing = await ExistingTablesAsync(context, ct);
        if (existing.Contains(ModuleDbContext.MigrationsHistoryTable))
            return;

        if (!TablesOf(context).Any(existing.Contains))
            return;

        var history = context.GetService<IHistoryRepository>();

        var initialMigration = context.Database.GetMigrations().First();
        await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        await context.Database.ExecuteSqlRawAsync(
            history.GetInsertScript(new HistoryRow(initialMigration, ProductInfo.GetVersion())), ct);

        logger.LogWarning(
            "Schema {Schema}: existing tables adopted, migration {Migration} marked as applied",
            context.Schema, initialMigration);
    }

    /// <summary>Logs model columns missing from the database (schema drift), so it cannot fail silently.</summary>
    private static async Task ReportMissingColumnsAsync(ModuleDbContext context, ILogger logger, CancellationToken ct)
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

        if (missing.Count > 0)
            logger.LogError("Schema {Schema} is missing columns used by the model: {Columns}",
                context.Schema, string.Join(", ", missing));
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
