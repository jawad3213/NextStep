using NextStep.Modules.Email.Repositories;
using NextStep.Modules.Email.Services;

namespace NextStep.Jobs;

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
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromHours(6);

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
    private readonly IGmailReplyMonitorService         _replyMonitor;
    private readonly IResponseClassificationService    _classifier;
    private readonly ILogger<CheckEmailRepliesJob>     _logger;

    public CheckEmailRepliesJob(
        IEmailDraftRepository         draftRepository,
        IGmailReplyMonitorService      replyMonitor,
        IResponseClassificationService classifier,
        ILogger<CheckEmailRepliesJob>  logger)
    {
        _draftRepository = draftRepository;
        _replyMonitor    = replyMonitor;
        _classifier      = classifier;
        _logger          = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("CheckEmailRepliesJob — started");

        // ── 1. Load all sent drafts pending reply check ─────────────────────────
        var drafts = await _draftRepository.GetPendingReplyCheckAsync(ct);

        _logger.LogInformation(
            "CheckEmailRepliesJob — {Count} draft(s) pending reply check", drafts.Count);

        int checked_    = 0;
        int repliesFound = 0;
        int errors       = 0;

        // ── 2. Process each draft ───────────────────────────────────────────────
        foreach (var draft in drafts)
        {
            var candidature = draft.Candidature;

            if (candidature is null)
            {
                _logger.LogWarning(
                    "CheckEmailRepliesJob — draft {DraftId} has no related candidature, skipping",
                    draft.Id);
                continue;
            }

            // ── 2a. Cooldown check ─────────────────────────────────────────────
            var needsReclassification =
                candidature.HasResponse &&
                string.Equals(candidature.ResponseStatus, "REPONSE_RECUE", StringComparison.OrdinalIgnoreCase) &&
                (!candidature.ResponseConfidence.HasValue || candidature.ResponseConfidence.Value <= 0.01);

            if (!needsReclassification &&
                candidature.LastCheckedAtUtc.HasValue &&
                DateTime.UtcNow - candidature.LastCheckedAtUtc.Value < CooldownPeriod)
            {
                _logger.LogDebug(
                    "CheckEmailRepliesJob — candidature {CandidatureId} checked recently, skipping",
                    candidature.IdCandidature);
                continue;
            }

            _logger.LogInformation(
                "CheckEmailRepliesJob — checking thread {ThreadId} for candidature {CandidatureId}",
                draft.ProviderThreadId, candidature.IdCandidature);

            // ── 2b. Check Gmail thread ─────────────────────────────────────────
            var result = await _replyMonitor.CheckThreadForReplyAsync(
                localUserId: candidature.IdUtilisateur,
                threadId:    draft.ProviderThreadId!,
                sentAtUtc:   draft.SentAtUtc!.Value,
                ct:          ct);

            checked_++;

            // ── 2c. Handle error ───────────────────────────────────────────────
            if (result.ErrorMessage is not null)
            {
                _logger.LogWarning(
                    "CheckEmailRepliesJob — error checking candidature {CandidatureId}: {Error}",
                    candidature.IdCandidature, result.ErrorMessage);
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
                    candidature.IdCandidature, result.ReplyFrom, result.ReplyDateUtc, result.ReplySubject);

                // STEP 1 — Always persist baseline fields first.
                //          These are GUARANTEED to be saved even if classification fails.
                candidature.HasResponse         = true;
                candidature.ResponseStatus      = "REPONSE_RECUE";
                candidature.Statut              = "REPONSE_RECUE";
                candidature.LastResponseAtUtc   = result.ReplyDateUtc;
                candidature.LastCheckedAtUtc    = DateTime.UtcNow;
                candidature.LastResponseFrom    = result.ReplyFrom;
                candidature.LastResponseSnippet = result.Snippet;
                repliesFound++;

                // STEP 2 — Attempt AI classification (never throws).
                var classification = await _classifier.ClassifyAsync(candidature, draft, result, ct);

                // STEP 3 — Apply classification result.
                //          Only override ResponseStatus/Statut when the LLM returned a
                //          specific classified type (not the "REPONSE_RECUE" fallback).
                if (ClassifiableTypes.Contains(classification.ResponseType))
                {
                    candidature.ResponseStatus = classification.ResponseType;
                    candidature.Statut         = classification.ResponseType;
                }
                // else: keep REPONSE_RECUE set in STEP 1

                // Always store analysis metadata (fallback values are still meaningful).
                candidature.ResponseSummary         = classification.Summary;
                candidature.RecommendedAction       = classification.RecommendedAction;
                candidature.ResponseConfidence      = classification.Confidence;
                candidature.ResponseClassifiedAtUtc = DateTime.UtcNow;
            }
            else
            {
                _logger.LogDebug(
                    "CheckEmailRepliesJob — no reply yet for candidature {CandidatureId}",
                    candidature.IdCandidature);

                candidature.LastCheckedAtUtc = DateTime.UtcNow;
            }
        }

        // ── 3. Persist all changes at once ──────────────────────────────────────
        await _draftRepository.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CheckEmailRepliesJob — completed. Checked: {Checked}, Replies found: {Replies}, Errors: {Errors}",
            checked_, repliesFound, errors);
    }
}
