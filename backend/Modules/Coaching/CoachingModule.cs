using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Coaching.Infrastructure.Persistence;
using NextStep.Modules.Coaching.Application.EventHandlers;
using NextStep.Modules.Coaching.Application.Services;
using NextStep.Modules.Coaching.Infrastructure.Agents;
using NextStep.Shared.Events;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Coaching;

/// <summary>
/// Coaching module: interview arena, salary coach and SN copilot (schema "coaching").
/// Dans Program.cs : builder.Services.AddCoachingModule(builder.Configuration);
/// </summary>
public static class CoachingModule
{
    public static IServiceCollection AddCoachingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<CoachingDbContext>(configuration, CoachingDbContext.SchemaName);

        // HttpClient typé vers Python FastAPI
        // BaseUrl lue depuis appsettings.json → AgentsService:BaseUrl
        services.AddHttpClient<IAgentHttpClient, AgentHttpClient>(client =>
        {
            var baseUrl = configuration["AgentsService:BaseUrl"]
                ?? throw new InvalidOperationException("AgentsService:BaseUrl not configured");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(60); // LLM peut prendre du temps
        }).AddHttpMessageHandler<NextStep.Shared.Http.AgentApiKeyHandler>();

        // Services métier
        services.AddScoped<IArenaService, ArenaService>();
        services.AddScoped<ISnCopilotService, SnCopilotService>();

        // Reactions to other modules' events
        services.AddScoped<IIntegrationEventHandler<CandidaturesDeleted>, DeleteSessionsOnCandidaturesDeleted>();

        return services;
    }
}
