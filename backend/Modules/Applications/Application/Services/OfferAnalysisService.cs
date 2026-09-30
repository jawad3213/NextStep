using System.Text.Json;
using Hangfire;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Jobs;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Applications.Application.Services;

public interface IOfferAnalysisService
{
    /// <summary>
    /// Runs the three agents synchronously (analyse offer → retrieve profile → match),
    /// stores the combined result on the offer and returns the analysis.
    /// </summary>
    Task<OfferAnalysisDto> AnalyzeAsync(Guid userId, Guid offerId, CancellationToken ct = default);

    /// <summary>
    /// Queues CV generation as a durable background job and returns immediately
    /// (progress goes through SignalR).
    /// </summary>
    void StartGenerationInBackground(Guid userId, Guid offerId, int templateId);

    /// <summary>
    /// Queues the three agents as a durable background job and reports the outcome
    /// through SignalR. Used by the HTTP entry point so the browser is never left holding
    /// an idle connection while the LLM runs.
    /// </summary>
    void StartAnalysisInBackground(Guid userId, Guid offerId);
}

public class OfferAnalysisService(
    IOfferService offerService,
    IAgentHttpClient agents,
    IBackgroundJobClient backgroundJobs,
    ILogger<OfferAnalysisService> logger) : IOfferAnalysisService
{
    public async Task<OfferAnalysisDto> AnalyzeAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        var offer = await offerService.GetOfferWithAnalysisAsync(userId, offerId, ct)
            ?? throw new NotFoundException("Offer not found.");

        try
        {
            var userIdStr = userId.ToString();

            // a) Agent 1: offer analysis
            var analyzeDoc = await agents.AnalyzeOfferAsync(offer.TexteBrut ?? "", userIdStr, ct);
            var analyzedOffer = analyzeDoc.RootElement.GetProperty("analyzed_offer");

            // b) Agents 2 & 3: profile retriever + skill-gap match
            var matchDoc = await agents.MatchProfileAsync(userIdStr, analyzedOffer, ct);

            // Merge the results into the stored pipeline format
            var combined = new Dictionary<string, object>
            {
                // Marks which run produced this payload, so a client waiting on a
                // background run can tell the new result from the previous one.
                { "analysis_run_id", Guid.NewGuid().ToString() },
                { "analyzed_offer", JsonSerializer.Deserialize<object>(analyzedOffer.GetRawText())! },
                { "skill_gap_analysis", JsonSerializer.Deserialize<object>(matchDoc.RootElement.GetRawText())! },
                { "match_result", JsonSerializer.Deserialize<object>(matchDoc.RootElement.GetRawText())! }
            };
            if (matchDoc.RootElement.TryGetProperty("profile_data", out var profileData))
                combined["profile_data"] = JsonSerializer.Deserialize<object>(profileData.GetRawText())!;

            using var pipelineResult = JsonDocument.Parse(JsonSerializer.Serialize(combined));
            await offerService.SavePipelineResultAsync(offerId, pipelineResult, userId, ct);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            logger.LogError(ex, "Synchronous analysis failed for offer {OfferId} / user {UserId}", offerId, userId);
            throw new OperationFailedException("Offer analysis failed. Please try again.", ex);
        }

        return await offerService.GetAnalysisAsync(userId, offerId, ct)
            ?? throw new OperationFailedException("Unable to retrieve the analysis after saving.");
    }

    public void StartGenerationInBackground(Guid userId, Guid offerId, int templateId)
    {
        backgroundJobs.Enqueue<OfferGenerationJob>(job => job.ExecuteAsync(offerId, userId.ToString(), templateId));
    }

    /// <summary>
    /// Queues the analysis off the request path and pushes progress/outcome to the browser
    /// over SignalR, the same channel the generation step already uses.
    /// </summary>
    /// <remarks>
    /// The three agents run in sequence and can take minutes, which is longer than the idle
    /// timeout of any reverse proxy in front of the API (Cloudflare, an ALB, a corporate
    /// proxy). Holding the HTTP request open for that long means the connection is dropped
    /// while the work is still running, and the user is shown a failure for a run that
    /// actually succeeded. Returning 202 immediately removes that failure mode entirely.
    /// The browser only joins the offer's SignalR group from the generation step onwards, so
    /// these events are best-effort: the client detects completion by polling the stored
    /// analysis, and uses these events for live feedback when it is already connected.
    /// The run itself is owned by Hangfire, so a restart no longer discards it.
    /// </remarks>
    public void StartAnalysisInBackground(Guid userId, Guid offerId)
    {
        backgroundJobs.Enqueue<OfferAnalysisJob>(job => job.ExecuteAsync(userId, offerId));
    }
}
