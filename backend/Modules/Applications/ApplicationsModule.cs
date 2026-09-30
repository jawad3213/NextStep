using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Applications.Infrastructure.Persistence;
using NextStep.Modules.Applications.Infrastructure.Repositories;
using NextStep.Modules.Applications.Application.Jobs;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Shared.Persistence;
using NextStep.Shared.Realtime;

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
        services.AddScoped<IOfferGroupAccess, OfferGroupAccess>();
        services.AddScoped<IAgentApplicationsService, AgentApplicationsService>();
        services.AddScoped<ICandidatureRepository, CandidatureRepository>();
        services.AddScoped<ICandidatureService, CandidatureService>();

        // Public contract
        services.AddScoped<IApplicationsApi, ApplicationsApi>();

        // Durable background jobs (Hangfire). Registered as scoped because Hangfire creates a
        // scope per job execution, which is what gives each job its own DbContext.
        services.AddScoped<OfferAnalysisJob>();
        services.AddScoped<OfferGenerationJob>();
        return services;
    }
}
