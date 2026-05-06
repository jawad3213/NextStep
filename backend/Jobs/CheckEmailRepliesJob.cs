using NextStep.Modules.Email.Repositories;
using NextStep.Modules.Email.Services;

namespace NextStep.Jobs;

/// <summary>
/// Hangfire recurring job that polls Gmail threads for recruiter replies
/// and updates candidature response status accordingly.
///
/// This job does NOT send any emails and does NOT generate follow-up drafts.
/// It only reads Gmail thread metadata and updates the local candidature record.
/// </summary>
public class CheckEmailRepliesJob
{
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromHours(6);

    private readonly IEmailDraftRepository _draftRepository;
    private readonly IGmailReplyMonitorService _replyMonitor;
    private readonly ILogger<CheckEmailRepliesJob> _logger;

    public CheckEmailRepliesJob(
        IEmailDraftRepository draftRepository,
        IGmailReplyMonitorService replyMonitor,
        ILogger<CheckEmailRepliesJob> logger)
    {
        _draftRepository = draftRepository;
        _replyMonitor    = replyMonitor;
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
            if (candidature.LastCheckedAtUtc.HasValue &&
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

            // ── 2c. Handle result ──────────────────────────────────────────────
            if (result.ErrorMessage is not null)
            {
                _logger.LogWarning(
                    "CheckEmailRepliesJob — error checking candidature {CandidatureId}: {Error}",
                    candidature.IdCandidature, result.ErrorMessage);
                errors++;
                // Do not update LastCheckedAtUtc on error — let it retry next run
                continue;
            }

            if (result.HasReply)
            {
                _logger.LogInformation(
                    "CheckEmailRepliesJob — reply detected for candidature {CandidatureId}: " +
                    "from={ReplyFrom}, date={ReplyDate}",
                    candidature.IdCandidature, result.ReplyFrom, result.ReplyDateUtc);

                candidature.HasResponse       = true;
                candidature.ResponseStatus    = "REPONSE_RECUE";
                candidature.Statut            = "REPONSE_RECUE";
                candidature.LastResponseAtUtc = result.ReplyDateUtc;
                candidature.LastCheckedAtUtc  = DateTime.UtcNow;
                repliesFound++;
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
