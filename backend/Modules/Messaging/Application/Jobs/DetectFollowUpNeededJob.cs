using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Shared.Config;

namespace NextStep.Modules.Messaging.Application.Jobs;

/// <summary>
/// Hangfire job that detects candidatures where no reply has been received 
/// after a certain delay and marks them as needing a follow-up (relance).
/// </summary>
public class DetectFollowUpNeededJob
{
    private readonly MessagingDbContext _db;
    private readonly IApplicationsApi _applications;
    private readonly EmailFollowUpOptions _options;
    private readonly ILogger<DetectFollowUpNeededJob> _logger;

    public DetectFollowUpNeededJob(
        MessagingDbContext db,
        IApplicationsApi applications,
        IOptions<EmailFollowUpOptions> options,
        ILogger<DetectFollowUpNeededJob> logger)
    {
        _db      = db;
        _applications = applications;
        _options = options.Value;
        _logger  = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("DetectFollowUpNeededJob — started");

        var initialDelay = _options.GetInitialDelay();
        var betweenDelay = _options.GetBetweenFollowUpDelay();
        var maxFollowUps = _options.MaxFollowUps;

        // ── 1. Find candidatures pending follow-up ─────────────────────────────
        // Conditions:
        // - HasResponse == false
        // - ResponseStatus is NOT REPONSE_RECUE, RELANCE_NECESSAIRE, or RELANCE_GENEREE
        // - At least one sent email exists
        // - No unsent relance draft already exists
        // The application state is owned by Applications; the drafts are Messaging's own data.
        var awaiting = await _applications.ListAwaitingFollowUpCheckAsync(ct);
        var awaitingIds = awaiting.Select(c => c.CandidatureId).ToList();

        var draftsByCandidature = (await _db.EmailDrafts
                .AsNoTracking()
                .Where(d => awaitingIds.Contains(d.CandidatureId))
                .ToListAsync(ct))
            .ToLookup(d => d.CandidatureId);

        var candidatures = awaiting
            .Where(c => draftsByCandidature[c.CandidatureId].Any(d => d.IsSent && d.SentAtUtc != null))
            .ToList();

        int markedCount = 0;

        foreach (var candidature in candidatures)
        {
            // Count sent relances
            var sentRelanceCount = draftsByCandidature[candidature.CandidatureId]
                .Count(d => d.EmailType == "relance" && d.IsSent);

            if (sentRelanceCount >= maxFollowUps)
                continue;

            // Check if any unsent relance draft exists
            var hasUnsentRelance = draftsByCandidature[candidature.CandidatureId]
                .Any(d => d.EmailType == "relance" && !d.IsSent);

            if (hasUnsentRelance)
                continue;

            // Get latest sent email
            var latestSent = draftsByCandidature[candidature.CandidatureId]
                .Where(d => d.IsSent && d.SentAtUtc != null)
                .OrderByDescending(d => d.SentAtUtc)
                .FirstOrDefault();

            if (latestSent == null)
                continue;

            // Threshold calculation
            var threshold = sentRelanceCount == 0 ? initialDelay : betweenDelay;

            if (DateTime.UtcNow - latestSent.SentAtUtc!.Value >= threshold)
            {
                _logger.LogInformation(
                    "DetectFollowUpNeededJob — marking candidature {CandidatureId} as RELANCE_NECESSAIRE (sentRelances: {Count})",
                    candidature.CandidatureId, sentRelanceCount);

                // Record the last time a relance was actually sent (for display context in frontend)
                var lastRelanceSent = draftsByCandidature[candidature.CandidatureId]
                    .Where(d => d.EmailType == "relance" && d.IsSent && d.SentAtUtc != null)
                    .OrderByDescending(d => d.SentAtUtc)
                    .FirstOrDefault();

                await _applications.MarkFollowUpNeededAsync(candidature.CandidatureId, lastRelanceSent?.SentAtUtc, ct);

                markedCount++;
            }
        }

        _logger.LogInformation(
            "DetectFollowUpNeededJob — completed. Marked: {Count}", markedCount);
    }
}
