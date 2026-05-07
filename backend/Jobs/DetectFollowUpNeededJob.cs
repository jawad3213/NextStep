using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.data;
using NextStep.Shared.Config;

namespace NextStep.Jobs;

/// <summary>
/// Hangfire job that detects candidatures where no reply has been received 
/// after a certain delay and marks them as needing a follow-up (relance).
/// </summary>
public class DetectFollowUpNeededJob
{
    private readonly AppDbContext _db;
    private readonly EmailFollowUpOptions _options;
    private readonly ILogger<DetectFollowUpNeededJob> _logger;

    public DetectFollowUpNeededJob(
        AppDbContext db,
        IOptions<EmailFollowUpOptions> options,
        ILogger<DetectFollowUpNeededJob> logger)
    {
        _db      = db;
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
        var candidatures = await _db.Candidatures
            .Include(c => c.EmailDrafts)
            .Where(c => 
                !c.HasResponse && 
                c.ResponseStatus != "REPONSE_RECUE" &&
                c.ResponseStatus != "RELANCE_NECESSAIRE" &&
                c.ResponseStatus != "RELANCE_GENEREE" &&
                c.EmailDrafts.Any(d => d.IsSent && d.SentAtUtc != null))
            .ToListAsync(ct);

        int markedCount = 0;

        foreach (var candidature in candidatures)
        {
            // Count sent relances
            var sentRelanceCount = candidature.EmailDrafts
                .Count(d => d.EmailType == "relance" && d.IsSent);

            if (sentRelanceCount >= maxFollowUps)
                continue;

            // Check if any unsent relance draft exists
            var hasUnsentRelance = candidature.EmailDrafts
                .Any(d => d.EmailType == "relance" && !d.IsSent);

            if (hasUnsentRelance)
                continue;

            // Get latest sent email
            var latestSent = candidature.EmailDrafts
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
                    candidature.IdCandidature, sentRelanceCount);

                candidature.ResponseStatus = "RELANCE_NECESSAIRE";
                candidature.Statut         = "RELANCE_NECESSAIRE";
                markedCount++;
            }
        }

        if (markedCount > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "DetectFollowUpNeededJob — completed. Marked: {Count}", markedCount);
    }
}
