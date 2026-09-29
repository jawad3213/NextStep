using Hangfire;
using NextStep.Modules.Applications.Application.Services;

namespace NextStep.Modules.Applications.Application.Jobs;

/// <summary>
/// Durable replacement for the previous fire-and-forget CV generation task.
/// </summary>
/// <remarks>
/// The generation step runs the full pipeline (up to ten minutes) and used to be started with
/// <c>Task.Run</c>, so a restart or a crash during that window discarded the work silently and
/// left the user on a spinner. Hangfire persists the job in PostgreSQL and re-runs an
/// interrupted one when the server comes back.
/// </remarks>
/// <remarks>
/// The runner keeps its own linked timeout, so no cancellation token is passed through the
/// serialized enqueue expression: Hangfire would store it as a plain argument rather than
/// inject the shutdown token, which would only give a misleading impression of being
/// cancellable. Automatic retries stay off for the same cost reason as the analysis job.
/// </remarks>
[AutomaticRetry(Attempts = 0)]
public class OfferGenerationJob(IPipelineRunnerService runner)
{
    public Task ExecuteAsync(Guid offerId, string userId, int templateId)
        => runner.StartGenerationAsync(offerId, userId, templateId, CancellationToken.None);
}
