using Microsoft.EntityFrameworkCore;

namespace NextStep.Shared.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module DbContext on the shared database, with its migrations
    /// history table inside the module's own schema.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : ModuleDbContext
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(ModuleDbContext.MigrationsHistoryTable, schema)));

        services.AddScoped<ModuleDbContext>(sp => sp.GetRequiredService<TContext>());
        return services;
    }
}
