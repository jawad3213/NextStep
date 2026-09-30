using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Shared.Http;
using NextStep.Shared.Realtime;

namespace NextStep.Modules.Applications.Application.Services;

public interface IPipelineRunnerService
{
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

    public async Task StartGenerationAsync(Guid offerId, string userId, int templateId, CancellationToken ct)
    {
        _logger.LogInformation("PipelineRunner — [GENERATION] Starting for offer {OfferId}", offerId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(10));
        var pipelineCt = cts.Token;

        try
        {
            Guid userGuid = Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : Guid.Empty;
            if (userGuid == Guid.Empty)
                throw new InvalidOperationException("Authenticated user id is missing or invalid.");

            await SendProgress(offerId, "generating_cv", "running", 10, "Generating optimized CV...", "cv_optimizer");
            var keepAliveTask = SendKeepAliveAsync(offerId, "generation", pipelineCt);

            using var scope = _scopeFactory.CreateScope();
            var offerService = scope.ServiceProvider.GetRequiredService<IOfferService>();
            var agentClient = scope.ServiceProvider.GetRequiredService<IAgentHttpClient>();

            // 1. Retrieve current analysis from DB
            var offer = await offerService.GetOfferWithAnalysisAsync(userGuid, offerId, CancellationToken.None);
            if (offer == null || string.IsNullOrEmpty(offer.AnalyseJson)) throw new Exception("Analysis data missing in DB");

            using var doc = JsonDocument.Parse(offer.AnalyseJson);
            var root = doc.RootElement;

            // 2. Prepare resume data for the agent
            var resumeData = new {
                analyzed_offer = root.TryGetProperty("analyzed_offer", out var ao) ? JsonSerializer.Deserialize<object>(ao.GetRawText()) : null,
                profile_data = root.TryGetProperty("profile_data", out var pd) ? JsonSerializer.Deserialize<object>(pd.GetRawText()) : null,
                skill_gap_analysis = root.TryGetProperty("skill_gap_analysis", out var sga)
                    ? JsonSerializer.Deserialize<object>(sga.GetRawText())
                    : root.TryGetProperty("match_result", out var mrForSkillGap)
                        ? JsonSerializer.Deserialize<object>(mrForSkillGap.GetRawText())
                        : null,
                match_result = root.TryGetProperty("match_result", out var mr)
                    ? JsonSerializer.Deserialize<object>(mr.GetRawText())
                    : root.TryGetProperty("skill_gap_analysis", out var sgaForMatch)
                        ? JsonSerializer.Deserialize<object>(sgaForMatch.GetRawText())
                        : null,
                company_intelligence = root.TryGetProperty("company_intelligence", out var ci) ? JsonSerializer.Deserialize<object>(ci.GetRawText()) : null
            };

            // 3. Run Pipeline with RESUME data and only_analysis=false
            var result = await agentClient.RunPipelineAsync(offer.TexteBrut ?? "", userId, templateId, offerId, onlyAnalysis: false, resumeData: resumeData, ct: pipelineCt);

            cts.Cancel();
            try { await keepAliveTask; } catch (OperationCanceledException) { }

            // 4. Save Final Result
            await offerService.SavePipelineResultAsync(offerId, result, userGuid, CancellationToken.None);

            var dto = await offerService.GetAnalysisAsync(userGuid, offerId, CancellationToken.None);
            
            await SendProgress(offerId, "generating_cv", "completed", 100, "CV generated successfully!", "db_persist");

            var completedEvent = new PipelineCompletedDto { OfferId = offerId, Status = "completed", Result = dto };
            await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineCompleted", completedEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PipelineRunner [GENERATION] — ❌ Error for {OfferId}", offerId);
            await _hubContext.Clients.Group(offerId.ToString()).SendAsync("PipelineCompleted", new PipelineCompletedDto { OfferId = offerId, Status = "error" });

            // The client has been told, but the failure must still reach Hangfire: swallowing it
            // here would make the dashboard report a successful run that produced no document.
            throw;
        }
    }

    /// <summary>
    /// Sends periodic keep-alive progress events every 15 seconds so the frontend
    /// knows the pipeline is still running and does not display a timeout error.
    /// </summary>
    private async Task SendKeepAliveAsync(Guid offerId, string mode, CancellationToken ct)
    {
        if (mode == "generation")
        {
            var generationSteps = new[]
            {
                (20, "Preparing candidate context...", "profile_context"),
                (38, "Consolidating existing analysis...", "analysis_context"),
                (65, "Optimizing CV...", "cv_optimizer"),
                (82, "Generating final CV...", "cv_engine"),
            };

            try
            {
                foreach (var (pct, msg, agent) in generationSteps)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), ct);
                    await SendProgress(offerId, "pipeline_running", "running", pct, msg, agent);
                }

                var generationHeartbeat = 85;
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), ct);
                    if (generationHeartbeat < 89) generationHeartbeat++;
                    await SendProgress(offerId, "pipeline_running", "running", generationHeartbeat,
                        "Finalizing...", "db_persist");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when pipeline completes - silently exit
            }

            return;
        }

        var steps = new[]
        {
            (15, "Analyzing the offer...", "offer_analyzer"),
            (25, "Retrieving candidate profile...", "profile_retriever"),
            (40, "Analyzing skills...", "skill_gap"),
            (55, "Company intelligence in progress...", "company_intel"),
            (70, "Optimizing CV...", "cv_optimizer"),
            (80, "Generating final CV...", "cv_engine"),
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
                    "Finalizing...", "db_persist");
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
}
