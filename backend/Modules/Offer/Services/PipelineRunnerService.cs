using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using NextStep.Modules.Offer.DTOs;
using NextStep.Shared.Http;
using NextStep.SignalR;

namespace NextStep.Modules.Offer.Services;

public interface IPipelineRunnerService
{
    Task StartAnalysisAsync(Guid offerId, string rawText, string userId, int templateId, CancellationToken ct);
    Task StartGenerationAsync(Guid offerId, string userId, int templateId, CancellationToken ct);
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

    public async Task StartAnalysisAsync(Guid offerId, string rawText, string userId, int templateId, CancellationToken ct)
    {
        _logger.LogInformation("PipelineRunner — [ANALYSIS] Starting for offer {OfferId}", offerId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(10));
        var pipelineCt = cts.Token;

        try
        {
            await SendProgress(offerId, "analyzing_offer", "running", 10, "Analyse initiale...", "offer_analyzer");
            var keepAliveTask = SendKeepAliveAsync(offerId, pipelineCt);

            using var scope = _scopeFactory.CreateScope();
            var agentClient = scope.ServiceProvider.GetRequiredService<IAgentHttpClient>();

            // Part 1: ONLY Analysis
            var result = await agentClient.RunPipelineAsync(rawText, userId, templateId, offerId, onlyAnalysis: true, ct: pipelineCt);

            cts.Cancel();
            try { await keepAliveTask; } catch (OperationCanceledException) { }

            // Save to DB
            var offerService = scope.ServiceProvider.GetRequiredService<IOfferService>();
            Guid userGuid = Guid.TryParse(userId, out var pg) ? pg : Guid.Empty;
            await offerService.SavePipelineResultAsync(offerId, result, userGuid, CancellationToken.None);

            var dto = await offerService.GetAnalysisAsync(offerId, CancellationToken.None);
            
            await SendProgress(offerId, "analyzing_offer", "completed", 100, "Analyse terminée. Choisissez un template.", "db_persist");

            var completedEvent = new PipelineCompletedDto { OfferId = offerId, Status = "completed", Result = dto };
            await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineCompleted", completedEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PipelineRunner [ANALYSIS] — ❌ Error for {OfferId}", offerId);
            await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineCompleted", new PipelineCompletedDto { OfferId = offerId, Status = "error" });
        }
    }

    public async Task StartGenerationAsync(Guid offerId, string userId, int templateId, CancellationToken ct)
    {
        _logger.LogInformation("PipelineRunner — [GENERATION] Starting for offer {OfferId}", offerId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(10));
        var pipelineCt = cts.Token;

        try
        {
            await SendProgress(offerId, "generating_cv", "running", 10, "Génération du CV optimisé...", "cv_optimizer");
            var keepAliveTask = SendKeepAliveAsync(offerId, pipelineCt);

            using var scope = _scopeFactory.CreateScope();
            var offerService = scope.ServiceProvider.GetRequiredService<IOfferService>();
            var agentClient = scope.ServiceProvider.GetRequiredService<IAgentHttpClient>();

            // 1. Retrieve current analysis from DB
            var offer = await offerService.GetOfferWithAnalysisAsync(offerId, CancellationToken.None);
            if (offer == null || string.IsNullOrEmpty(offer.AnalyseJson)) throw new Exception("Analysis data missing in DB");

            using var doc = JsonDocument.Parse(offer.AnalyseJson);
            var root = doc.RootElement;

            // 2. Prepare resume data for the agent
            var resumeData = new {
                analyzed_offer = root.TryGetProperty("analyzed_offer", out var ao) ? JsonSerializer.Deserialize<object>(ao.GetRawText()) : null,
                profile_data = root.TryGetProperty("profile_data", out var pd) ? JsonSerializer.Deserialize<object>(pd.GetRawText()) : null,
                match_result = root.TryGetProperty("match_result", out var mr) ? JsonSerializer.Deserialize<object>(mr.GetRawText()) : null,
                company_intelligence = root.TryGetProperty("company_intelligence", out var ci) ? JsonSerializer.Deserialize<object>(ci.GetRawText()) : null
            };

            // 3. Run Pipeline with RESUME data and only_analysis=false
            var result = await agentClient.RunPipelineAsync(offer.TexteBrut ?? "", userId, templateId, offerId, onlyAnalysis: false, resumeData: resumeData, ct: pipelineCt);

            cts.Cancel();
            try { await keepAliveTask; } catch (OperationCanceledException) { }

            // 4. Save Final Result
            Guid userGuid = Guid.TryParse(userId, out var pg) ? pg : Guid.Empty;
            await offerService.SavePipelineResultAsync(offerId, result, userGuid, CancellationToken.None);

            var dto = await offerService.GetAnalysisAsync(offerId, CancellationToken.None);
            
            await SendProgress(offerId, "generating_cv", "completed", 100, "CV généré avec succès !", "db_persist");

            var completedEvent = new PipelineCompletedDto { OfferId = offerId, Status = "completed", Result = dto };
            await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineCompleted", completedEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PipelineRunner [GENERATION] — ❌ Error for {OfferId}", offerId);
            await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineCompleted", new PipelineCompletedDto { OfferId = offerId, Status = "error" });
        }
    }

    /// <summary>
    /// Sends periodic keep-alive progress events every 15 seconds so the frontend
    /// knows the pipeline is still running and does not display a timeout error.
    /// </summary>
    private async Task SendKeepAliveAsync(Guid offerId, CancellationToken ct)
    {
        var steps = new[]
        {
            (15, "Analyse de l'offre en cours...", "offer_analyzer"),
            (25, "Récupération du profil candidat...", "profile_retriever"),
            (40, "Analyse des compétences...", "skill_gap"),
            (55, "Intelligence entreprise en cours...", "company_intel"),
            (70, "Optimisation du CV...", "cv_optimizer"),
            (80, "Génération du CV final...", "cv_engine"),
        };

        try
        {
            foreach (var (pct, msg, agent) in steps)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
                await SendProgress(offerId, "pipeline_running", "running", pct, msg, agent);
            }

            // After all named steps, keep sending heartbeats every 20s
            var heartbeat = 85;
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
                if (heartbeat < 89) heartbeat++;
                await SendProgress(offerId, "pipeline_running", "running", heartbeat,
                    "Finalisation en cours...", "db_persist");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when pipeline completes — silently exit
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
            dto.AnneesExperience = ao.GetStringAsIntOrDefault("annees_experience");
            dto.NiveauEtudes = ao.GetStringOrDefault("niveau_etudes");
            dto.ModeTravail = ao.GetStringOrDefault("mode_travail");
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
            dto.ScoreAts = sg.GetIntOrDefault("score_ats") ?? dto.ScoreAts;
            dto.KeywordsPresents = sg.GetStringList("keywords_presents").Count > 0 ? sg.GetStringList("keywords_presents") : dto.KeywordsPresents;
            dto.KeywordsManquants = sg.GetStringList("keywords_manquants").Count > 0 ? sg.GetStringList("keywords_manquants") : dto.KeywordsManquants;
            dto.Recommandations = sg.GetStringList("recommandations").Count > 0 ? sg.GetStringList("recommandations") : dto.Recommandations;
            dto.CompetencesMatching = sg.GetStringList("competences_matching").Count > 0 ? sg.GetStringList("competences_matching") : dto.CompetencesMatching;
            dto.CompetencesManquantes = sg.GetStringList("competences_manquantes").Count > 0 ? sg.GetStringList("competences_manquantes") : dto.CompetencesManquantes;
        }

        if (root.TryGetProperty("company_intelligence", out var ci) && ci.ValueKind == JsonValueKind.Object)
        {
            var intelligence = ci.GetPropertyOrNull("intelligence");
            if (intelligence.HasValue)
            {
                var culture = intelligence.Value.GetPropertyOrNull("culture");
                if (culture.HasValue)
                {
                    dto.CompanyCultureScore = culture.Value.GetDoubleOrDefault("glassdoor_rating") ?? culture.Value.GetDoubleOrDefault("culture_score") ?? 0;
                }

                var salaries = intelligence.Value.GetPropertyOrNull("salaries");
                if (salaries.HasValue && salaries.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in salaries.Value.EnumerateArray())
                    {
                        dto.CompanySalaryMin = s.GetIntOrDefault("min_salary") ?? dto.CompanySalaryMin;
                        dto.CompanySalaryMax = s.GetIntOrDefault("max_salary") ?? dto.CompanySalaryMax;
                    }
                }

                var actualites = intelligence.Value.GetPropertyOrNull("actualites");
                if (actualites.HasValue && actualites.Value.ValueKind == JsonValueKind.Array)
                {
                    dto.CompanyNews = [.. actualites.Value.EnumerateArray()
                        .Where(a => a.ValueKind == JsonValueKind.String)
                        .Select(a => new CompanyNewsItem { Title = a.GetString() ?? "", Date = "" })];
                }
            }
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
