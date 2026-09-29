using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Modules.Messaging.Application.Services;
using NextStep.Modules.Messaging.Infrastructure.Gmail;

namespace NextStep.Modules.Messaging.Application.Jobs;

/// <summary>
/// Hangfire recurring job that polls Gmail threads for recruiter replies
/// and updates candidature response status accordingly.
///
/// Phase 3A: after a reply is detected, the job calls IResponseClassificationService
/// to classify the reply via the Python LLM agent. Classification failure is fully
/// isolated — it never prevents HasResponse=true from being persisted.
///
/// This job does NOT send any emails and does NOT generate follow-up drafts.
/// </summary>
public class CheckEmailRepliesJob
{
    // LLM-returned types that can override ResponseStatus/Statut.
    // "REPONSE_RECUE" is the fallback — it is never returned by the LLM directly.
    private static readonly HashSet<string> ClassifiableTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ENTRETIEN_PROPOSE",
            "INFORMATIONS_DEMANDEES",
            "ACCEPTE",
            "REFUSE",
            "REPONSE_AUTOMATIQUE",
            "REPONSE_GENERALE",
            "INCONNU",
        };

    private readonly IEmailDraftRepository            _draftRepository;
    private readonly IApplicationsApi                  _applications;
    private readonly IGmailReplyMonitorService         _replyMonitor;
    private readonly IResponseClassificationService    _classifier;
    private readonly ILogger<CheckEmailRepliesJob>     _logger;

    public CheckEmailRepliesJob(
        IEmailDraftRepository         draftRepository,
        IApplicationsApi              applications,
        IGmailReplyMonitorService      replyMonitor,
        IResponseClassificationService classifier,
        ILogger<CheckEmailRepliesJob>  logger)
    {
        _draftRepository = draftRepository;
        _applications    = applications;
        _replyMonitor    = replyMonitor;
        _classifier      = classifier;
        _logger          = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("CheckEmailRepliesJob — started");

        // ── 1. Load all sent drafts pending reply check ─────────────────────────
        var sentDrafts = await _draftRepository.GetPendingReplyCheckAsync(ct);

        var applications = (await _applications.GetApplicationsAsync(
                sentDrafts.Select(d => d.CandidatureId).Distinct().ToList(), ct))
            .ToDictionary(a => a.CandidatureId);

        var drafts = sentDrafts
            .Where(d => applications.TryGetValue(d.CandidatureId, out var a) && AwaitsReplyCheck(a))
            .ToList();

        _logger.LogInformation(
            "CheckEmailRepliesJob — {Count} draft(s) pending reply check", drafts.Count);

        int checked_    = 0;
        int repliesFound = 0;
        int errors       = 0;

        // ── 2. Process each draft ───────────────────────────────────────────────
        foreach (var draft in drafts)
        {
            if (!applications.TryGetValue(draft.CandidatureId, out var candidature))
            {
                _logger.LogWarning(
                    "CheckEmailRepliesJob — draft {DraftId} has no related candidature, skipping",
                    draft.Id);
                continue;
            }

            // ── 2a. Cooldown check ─────────────────────────────────────────────
            // We no longer skip based on CooldownPeriod so that manual "Trigger Now"
            // always works. The automated frequency is controlled by the Hangfire cron schedule.

            _logger.LogInformation(
                "CheckEmailRepliesJob — checking thread {ThreadId} for candidature {CandidatureId}",
                draft.ProviderThreadId, candidature.CandidatureId);

            // ── 2b. Check Gmail thread ─────────────────────────────────────────
            var result = await _replyMonitor.CheckThreadForReplyAsync(
                localUserId: candidature.UserId,
                threadId:    draft.ProviderThreadId!,
                sentAtUtc:   draft.SentAtUtc!.Value,
                ct:          ct);

            checked_++;

            // ── 2c. Handle error ───────────────────────────────────────────────
            if (result.ErrorMessage is not null)
            {
                _logger.LogWarning(
                    "CheckEmailRepliesJob — error checking candidature {CandidatureId}: {Error}",
                    candidature.CandidatureId, result.ErrorMessage);
                errors++;
                // Do not update LastCheckedAtUtc on error — let it retry next run
                continue;
            }

            // ── 2d. Reply detected ─────────────────────────────────────────────
            if (result.HasReply)
            {
                _logger.LogInformation(
                    "CheckEmailRepliesJob — reply detected for candidature {CandidatureId}: " +
                    "from={ReplyFrom}, date={ReplyDate}, subject={ReplySubject}",
                    candidature.CandidatureId, result.ReplyFrom, result.ReplyDateUtc, result.ReplySubject);

                repliesFound++;

                // STEP 1 — Attempt AI classification (never throws).
                var offer = candidature.OfferId.HasValue
                    ? await _applications.GetOfferContentAsync(candidature.OfferId.Value, ct)
                    : null;
                var classification = await _classifier.ClassifyAsync(candidature, offer?.AnalysisJson, draft, result, ct);

                // STEP 2 — Only override the status when the LLM returned a specific
                //          classified type (not the "REPONSE_RECUE" fallback).
                var status = ClassifiableTypes.Contains(classification.ResponseType)
                    ? classification.ResponseType
                    : "REPONSE_RECUE";

                // STEP 3 — Persist the reply and its analysis (fallback values are still meaningful).
                await _applications.RecordRecruiterReplyAsync(candidature.CandidatureId, new RecruiterReply(
                    Status:            status,
                    CheckedAtUtc:      DateTime.UtcNow,
                    ReplyAtUtc:        result.ReplyDateUtc,
                    From:              result.ReplyFrom,
                    Snippet:           result.Snippet,
                    Summary:           classification.Summary,
                    RecommendedAction: classification.RecommendedAction,
                    Confidence:        classification.Confidence,
                    ClassifiedAtUtc:   DateTime.UtcNow), ct);
            }
            else
            {
                _logger.LogDebug(
                    "CheckEmailRepliesJob — no reply yet for candidature {CandidatureId}",
                    candidature.CandidatureId);

                await _applications.RecordReplyCheckAsync(candidature.CandidatureId, DateTime.UtcNow, ct);
            }
        }

        _logger.LogInformation(
            "CheckEmailRepliesJob — completed. Checked: {Checked}, Replies found: {Replies}, Errors: {Errors}",
            checked_, repliesFound, errors);
    }

    /// <summary>No reply yet, or a reply whose classification failed and should be retried.</summary>
    private static bool AwaitsReplyCheck(ApplicationSnapshot application) =>
        !application.HasResponse ||
        (string.Equals(application.ResponseStatus, "REPONSE_RECUE", StringComparison.OrdinalIgnoreCase) &&
         (!application.ResponseConfidence.HasValue || application.ResponseConfidence.Value <= 0.01));
}
