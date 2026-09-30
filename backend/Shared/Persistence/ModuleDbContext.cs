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

    /// <summary>PostgreSQL schema owned by the module.</summary>
    public abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        ConfigureModel(modelBuilder);
    }

    /// <summary>
    /// Every timestamp of this application is UTC, on both sides: the C# services only ever use
    /// <c>DateTime.UtcNow</c> and the Python agents write <c>utc_now()</c>, which is a naive UTC
    /// value.
    /// </summary>
    /// <remarks>
    /// The columns are "timestamp without time zone", so PostgreSQL stores no offset and the
    /// convention has to be declared rather than inherited. Npgsql maps <see cref="DateTime"/> to
    /// "timestamp with time zone" by default, and this application used to get the other mapping
    /// from a process-wide <c>AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior")</c>.
    /// That switch was a global side effect: it also disabled Npgsql's strict UTC validation for
    /// every "timestamp with time zone" column in the process, including Hangfire's job storage,
    /// which is where a local time would have been silently accepted and shifted.
    /// Declaring the convention here keeps the strict validation on and states the storage
    /// convention in the one place that owns it. It is registered through
    /// <see cref="ConfigureConventions"/> rather than by walking the built model, so it also
    /// covers owned types without mutating the model while it is being enumerated.
    /// </remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
        base.ConfigureConventions(configurationBuilder);
    }

    protected abstract void ConfigureModel(ModelBuilder modelBuilder);
}
