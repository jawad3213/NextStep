using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Applications.Infrastructure.Persistence;
using NextStep.Modules.Applications.Infrastructure.Repositories;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Applications;

/// <summary>Applications module: job offers, applications and their documents (schema "applications").</summary>
public static class ApplicationsModule
{
    public static IServiceCollection AddApplicationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ApplicationsDbContext>(configuration, ApplicationsDbContext.SchemaName);

        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<IOfferAnalysisService, OfferAnalysisService>();
        services.AddScoped<IPipelineRunnerService, PipelineRunnerService>();
        services.AddScoped<ISkillGapService, SkillGapService>();
        services.AddScoped<ICandidatureRepository, CandidatureRepository>();
        services.AddScoped<ICandidatureService, CandidatureService>();

        // Public contract
        services.AddScoped<IApplicationsApi, ApplicationsApi>();
        return services;
    }
}
