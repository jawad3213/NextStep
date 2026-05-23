using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NextStep.Modules.Chatbot.Interfaces;
using NextStep.Modules.Chatbot.Services;

namespace NextStep.Modules.Chatbot;

/// <summary>
/// Point d'entrée du module Chatbot.
/// Dans Program.cs : builder.Services.AddChatbotModule(builder.Configuration);
/// </summary>
public static class ChatbotModule
{
    public static IServiceCollection AddChatbotModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // HttpClient typé vers Python FastAPI
        // BaseUrl lue depuis appsettings.json → AgentsService:BaseUrl
        services.AddHttpClient<IAgentHttpClient, AgentHttpClient>(client =>
        {
            var baseUrl = configuration["AgentsService:BaseUrl"]
                ?? throw new InvalidOperationException("AgentsService:BaseUrl not configured");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(60); // LLM peut prendre du temps
        });

        // Service métier
        services.AddScoped<IArenaService, ArenaService>();

        return services;
    }
}