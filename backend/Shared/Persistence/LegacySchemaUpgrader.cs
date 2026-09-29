using Microsoft.EntityFrameworkCore;

namespace NextStep.Shared.Persistence;

/// <summary>
/// One-time upgrade of databases created before the modular split, where every table lived
/// in "public" with foreign keys between modules. Idempotent: does nothing once upgraded.
///   1. moves each module's tables into the module's schema (data, indexes and constraints move along);
///   2. drops foreign keys that cross schemas: modules reference each other by id only.
/// Tables owned by the Python agents are moved by the agents themselves ("agents" schema).
/// </summary>
public static class LegacySchemaUpgrader
{
    /// <summary>Schemas whose foreign keys must stay inside the schema.</summary>
    private static readonly string[] ExtraIsolatedSchemas = ["public", "agents"];

    public static async Task UpgradeAsync(IReadOnlyList<ModuleDbContext> contexts, ILogger logger, CancellationToken ct = default)
    {
        if (contexts.Count == 0) return;
        var database = contexts[0].Database;

        foreach (var context in contexts)
        {
            // Schema and table names come from the EF model, never from input.
            var createSchema = $"CREATE SCHEMA IF NOT EXISTS {context.Schema};";
            await database.ExecuteSqlRawAsync(createSchema, ct);

            foreach (var table in DatabaseInitializer.TablesOf(context))
            {
                var moveTable = $@"
                    DO $$
                    BEGIN
                        IF to_regclass('public.{table}') IS NOT NULL AND to_regclass('{context.Schema}.{table}') IS NULL THEN
                            ALTER TABLE public.{table} SET SCHEMA {context.Schema};
                        END IF;
                    END $$;";
                await database.ExecuteSqlRawAsync(moveTable, ct);
            }
        }

        var schemas = contexts.Select(c => c.Schema).Concat(ExtraIsolatedSchemas).Distinct();
        var schemaList = string.Join(", ", schemas.Select(s => $"'{s}'"));

        var dropCrossSchemaForeignKeys = $@"
            DO $$
            DECLARE r record;
            BEGIN
                FOR r IN
                    SELECT c.conname, n.nspname AS schema_name, t.relname AS table_name
                    FROM pg_constraint c
                    JOIN pg_class t      ON t.oid = c.conrelid
                    JOIN pg_namespace n  ON n.oid = t.relnamespace
                    JOIN pg_class rt     ON rt.oid = c.confrelid
                    JOIN pg_namespace rn ON rn.oid = rt.relnamespace
                    WHERE c.contype = 'f'
                      AND n.nspname <> rn.nspname
                      AND n.nspname IN ({schemaList})
                LOOP
                    EXECUTE format('ALTER TABLE %I.%I DROP CONSTRAINT %I', r.schema_name, r.table_name, r.conname);
                    RAISE NOTICE 'dropped cross-module foreign key %.%.%', r.schema_name, r.table_name, r.conname;
                END LOOP;
            END $$;";
        await database.ExecuteSqlRawAsync(dropCrossSchemaForeignKeys, ct);

        logger.LogInformation("Module schemas checked: {Schemas}", string.Join(", ", schemas));
    }
}
