using NextStep.Modules.Coaching.Infrastructure.Agents;
using Microsoft.Extensions.Logging;
using NextStep.Modules.Coaching.Application.Dtos;
using NextStep.Modules.Coaching.Application.Services;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Coaching.Application.Services;

public class SnCopilotService : ISnCopilotService
{
    private readonly IAgentHttpClient _agentClient;
    private readonly ILogger<SnCopilotService> _logger;

    public SnCopilotService(IAgentHttpClient agentClient, ILogger<SnCopilotService> logger)
    {
        _agentClient = agentClient;
        _logger = logger;
    }

    public async Task<SnChatAgentResponse> ChatAsync(
        Guid userId,
        string userName,
        SnUserChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new BadRequestException("Le message ne peut pas être vide.");

        _logger.LogInformation("SN Copilot chat initiated for user {UserId} ({UserName})", userId, userName);

        var agentRequest = new SnChatAgentRequest(
            UserId: userId.ToString(),
            Message: request.Message,
            UserName: userName,
            History: request.History
        );

        try
        {
            return await _agentClient.PostSnChatAsync(agentRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'interaction avec SN Copilot.");
            throw new OperationFailedException("Erreur de communication avec le copilote SN.", ex);
        }
    }

    public Task<List<SnStarterSuggestionItem>> GetStarterSuggestionsAsync()
    {
        var starters = new List<SnStarterSuggestionItem>
        {
            new(
                Title: "Mon Profil",
                Description: "Résumé complet de vos compétences, formations et expériences",
                Prompt: "Résume mon profil complet avec mes compétences et expériences",
                Category: "Profil",
                Icon: "user"
            ),
            new(
                Title: "CV Matching",
                Description: "Analyse la compatibilité de votre profil avec vos candidatures",
                Prompt: "Match mon profil avec mes candidatures actives",
                Category: "Intelligence",
                Icon: "target"
            ),
            new(
                Title: "Portfolio Review",
                Description: "Visualiser vos 10 dernières candidatures et statuts récents",
                Prompt: "Affiche mes 10 dernières candidatures avec leurs statuts et dates",
                Category: "Portfolio",
                Icon: "layers"
            ),
            new(
                Title: "Priorités de Relance",
                Description: "Identifier les candidatures stagnantes nécessitant un suivi",
                Prompt: "Quelles sont les candidatures sans réponse à relancer en priorité ?",
                Category: "Stratégie",
                Icon: "clock"
            ),
            new(
                Title: "Métriques & Vélocité",
                Description: "Synthèse exécutive du taux de réponse et conversion",
                Prompt: "Analyse mes taux de conversion et les statistiques de mon pipeline",
                Category: "Analytics",
                Icon: "bar-chart-2"
            ),
            new(
                Title: "Mutation Rapide",
                Description: "Modifier un statut de candidature ou enregistrer un entretien",
                Prompt: "Passe ma dernière candidature en statut 'Entretien'",
                Category: "Action",
                Icon: "check-circle"
            )
        };

        return Task.FromResult(starters);
    }
}
