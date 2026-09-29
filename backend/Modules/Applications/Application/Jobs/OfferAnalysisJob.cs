using Hangfire;
using Microsoft.AspNetCore.SignalR;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Shared.Realtime;

namespace NextStep.Modules.Applications.Application.Jobs;

/// <summary>
/// Durable replacement for the previous fire-and-forget analysis task.
/// </summary>
/// <remarks>
/// <para>
/// This job used to be started with <c>Task.Run</c> from the HTTP entry point, which left the
/// process as the only owner of a run lasting several minutes. A container restart, a crash or a
/// scale-down silently killed the run: no retry, no trace, and the browser kept polling a result
/// that would never arrive. Hangfire persists the job in PostgreSQL, so an interrupted run is
/// picked up again when the server comes back and shows up in the /hangfire dashboard.
/// </para>
/// <para>
/// Automatic retries are disabled on purpose. Every attempt calls the paid LLM agents, and the
/// failures we actually see (bad agent payload, missing configuration, refused connection) are
/// deterministic: retrying them only re-bills the user and delays the error. The browser offers
/// an explicit retry instead.
/// </para>
/// <para>
/// The failure is rethrown after the client has been notified so Hangfire records the job as
/// Failed instead of reporting a successful run that produced nothing.
/// </para>
/// </remarks>
[AutomaticRetry(Attempts = 0)]
public class OfferAnalysisJob(
    IOfferAnalysisService analysisService,
    IHubContext<PipelineHub> hubContext,
    ILogger<OfferAnalysisJob> logger)
{
    public async Task ExecuteAsync(Guid userId, Guid offerId)
    {
        var group = offerId.ToString();

        try
        {
            // AnalyzeAsync stores the merged result on the offer and hands it back,
            // so there is no need to re-read it from the database.
            var result = await analysisService.AnalyzeAsync(userId, offerId, CancellationToken.None);

            await hubContext.Clients.Group(group).SendAsync("PipelineProgress", new
            {
                offerId,
                step = "analysis",
                status = "done",
                progressPercent = 100,
                message = "Analysis complete",
                agentName = "skill_gap"
            }, CancellationToken.None);

            await hubContext.Clients.Group(group).SendAsync("PipelineCompleted", new
            {
                offerId,
                status = "completed",
                result
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis [BACKGROUND] failed for offer {OfferId}", offerId);

            // The browser is polling the analysis, so a failure to notify is not fatal
            // for the run itself; it must not mask the original error either.
            try
            {
                await hubContext.Clients.Group(group).SendAsync("PipelineCompleted", new
                {
                    offerId,
                    status = "error",
                    error = "Offer analysis failed. Please try again."
                }, CancellationToken.None);
            }
            catch (Exception notifyEx)
            {
                logger.LogError(notifyEx, "Could not push the analysis failure for offer {OfferId}", offerId);
            }

            throw;
        }
    }
}
