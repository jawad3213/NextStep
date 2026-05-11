using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using NextStep.Modules.Offer.DTOs;
using NextStep.Shared.Http;
using NextStep.SignalR;

namespace NextStep.Modules.Offer.Services;

public interface IPipelineRunnerService
{
    Task RunPipelineAsync(string rawText, string userId, int templateId, Guid offerId, CancellationToken ct = default);
}

public class PipelineRunnerService : IPipelineRunnerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PipelineHub> _hubContext;
    private readonly ILogger<PipelineRunnerService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public PipelineRunnerService(
        IServiceScopeFactory scopeFactory,
        IHubContext<PipelineHub> hubContext,
        ILogger<PipelineRunnerService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task RunPipelineAsync(string rawText, string userId, int templateId, Guid offerId, CancellationToken ct = default)
    {
        _logger.LogInformation("PipelineRunner — Starting pipeline for offer {OfferId}", offerId);

        try
        {
            await SendProgress(offerId, "analyzing_offer", "running", 5, "Analyse de l'offre par IA...", "offer_analyzer");

            using var scope = _scopeFactory.CreateScope();
            var agentClient = scope.ServiceProvider.GetRequiredService<IAgentHttpClient>();

            var pipelineResult = await agentClient.RunPipelineAsync(rawText, userId, templateId, offerId, ct);

            var root = pipelineResult.RootElement;

            await SendProgress(offerId, "saving_results", "running", 90, "Sauvegarde des résultats...", "db_persist");

            var offerService = scope.ServiceProvider.GetRequiredService<IOfferService>();
            Guid userGuid = Guid.TryParse(userId, out var pg) ? pg : Guid.Empty;
            await offerService.SavePipelineResultAsync(offerId, pipelineResult, userGuid, CancellationToken.None);

            var dto = await offerService.GetAnalysisAsync(offerId, CancellationToken.None) ?? MapToDto(offerId, root);

            await SendProgress(offerId, "saving_results", "completed", 100, "Pipeline terminé avec succès", "db_persist");

            var completedEvent = new PipelineCompletedDto
            {
                OfferId = offerId,
                Status = "completed",
                Result = dto
            };

            await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineCompleted", completedEvent, CancellationToken.None);

            _logger.LogInformation("PipelineRunner — ✅ Pipeline done for offer {OfferId}", offerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PipelineRunner — ❌ Pipeline failed for offer {OfferId}", offerId);

            var errorEvent = new PipelineCompletedDto
            {
                OfferId = offerId,
                Status = "error",
                Error = ex.Message
            };

            await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineError", errorEvent, CancellationToken.None);
        }
    }

    private async Task SendProgress(Guid offerId, string step, string status, int progressPercent, string? message = null, string? agentName = null)
    {
        var dto = new PipelineProgressDto
        {
            OfferId = offerId,
            Step = step,
            Status = status,
            ProgressPercent = progressPercent,
            Message = message,
            AgentName = agentName
        };

        await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineProgress", dto);
    }

    private static OfferAnalysisDto MapToDto(Guid offerId, JsonElement root)
    {
        var dto = new OfferAnalysisDto { OfferId = offerId };

        if (root.TryGetProperty("analyzed_offer", out var ao) && ao.ValueKind == JsonValueKind.Object)
        {
            dto.Titre = ao.GetStringOrDefault("titre") ?? "";
            dto.Entreprise = ao.GetStringOrDefault("entreprise");
            dto.TypeContrat = ao.GetStringOrDefault("type_contrat");
            dto.Localisation = ao.GetStringOrDefault("localisation");
            dto.DescriptionPoste = ao.GetStringOrDefault("description_poste");
            dto.AnneesExperience = ao.GetIntOrDefault("annees_experience");
            dto.NiveauEtudes = ao.GetStringOrDefault("niveau_etudes");
            dto.CompetencesRequises = ao.GetStringList("competences_requises");
            dto.CompetencesSouhaitees = ao.GetStringList("competences_souhaitees");
            dto.KeywordsAts = ao.GetStringList("keywords_ats");
        }

        if (root.TryGetProperty("match_result", out var mr) && mr.ValueKind == JsonValueKind.Object)
        {
            dto.ScoreMatching = mr.GetIntOrDefault("score_matching") ?? 0;
            dto.ScoreAts = mr.GetIntOrDefault("score_ats") ?? 0;
            dto.KeywordsPresents = mr.GetStringList("keywords_presents");
            dto.KeywordsManquants = mr.GetStringList("keywords_manquants");
            dto.Recommandations = mr.GetStringList("recommandations");
            dto.CompetencesMatching = mr.GetStringList("competences_matching");
            dto.CompetencesManquantes = mr.GetStringList("competences_manquantes");
        }

        if (root.TryGetProperty("skill_gap", out var sg) && sg.ValueKind == JsonValueKind.Object)
        {
            dto.ScoreMatching = sg.GetIntOrDefault("score_matching") ?? dto.ScoreMatching;
        }

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            dto.Erreurs = [.. errors.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)];
        }

        return dto;
    }
}
