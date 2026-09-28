using NextStep.Modules.Sourcing.Infrastructure.Persistence;
using NextStep.Modules.Sourcing.Application.Services;
using NextStep.Modules.Sourcing.Infrastructure.JobBoards;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Sourcing;

/// <summary>Sourcing module: job-board scraping sessions and sourced offers (schema "sourcing").</summary>
public static class SourcingModule
{
    public static IServiceCollection AddSourcingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SourcingDbContext>(configuration, SourcingDbContext.SchemaName);
        services.AddScoped<JobProviderClient>();
        services.AddScoped<ISourcedOfferService, SourcedOfferService>();
        return services;
    }
}
