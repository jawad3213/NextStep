using Microsoft.EntityFrameworkCore;

namespace NextStep.Shared.Persistence;

/// <summary>
/// Base class of every module's DbContext. Each module owns one PostgreSQL schema,
/// maps only its own tables and keeps its own EF migrations history in that schema.
/// Tables of other modules are never mapped: cross-module data goes through Contracts.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    static ModuleDbContext()
    {
        // Columns are "timestamp without time zone"; keep the same mapping for design-time tools.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    /// <summary>PostgreSQL schema owned by the module.</summary>
    public abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        ConfigureModel(modelBuilder);
    }

    protected abstract void ConfigureModel(ModelBuilder modelBuilder);
}
