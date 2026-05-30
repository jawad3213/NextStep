namespace NextStep.Modules.Email.Services;

/// <summary>
/// Checks a Gmail thread for replies using the Gmail threads API (metadata format).
/// Only reads thread metadata — does not write, delete, or read full email bodies.
/// </summary>
public interface IGmailReplyMonitorService
{
    /// <summary>
    /// Checks whether any external reply exists in the Gmail thread identified by <paramref name="threadId"/>.
    /// A message is considered a reply if its internalDate is after <paramref name="sentAtUtc"/>
    /// and the sender is not the connected Gmail account.
    /// </summary>
    /// <param name="localUserId">The local DB user whose Gmail connection should be used.</param>
    /// <param name="threadId">The Gmail thread ID to inspect.</param>
    /// <param name="sentAtUtc">The UTC timestamp when the email was sent (used to filter out the original message).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ReplyCheckResult> CheckThreadForReplyAsync(
        Guid localUserId,
        string threadId,
        DateTime sentAtUtc,
        CancellationToken ct = default);
}
