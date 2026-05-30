namespace NextStep.Modules.Email.Services;

/// <summary>
/// Result of checking a Gmail thread for external replies.
/// </summary>
public class ReplyCheckResult
{
    /// <summary>True if a reply from a non-sender address was found in the thread.</summary>
    public bool HasReply { get; set; }

    /// <summary>UTC timestamp of the reply message. Null if no reply was found.</summary>
    public DateTime? ReplyDateUtc { get; set; }

    /// <summary>From address of the reply message. Null if no reply was found.</summary>
    public string? ReplyFrom { get; set; }

    /// <summary>Short snippet from the reply for logging only. Not exposed to the frontend.</summary>
    public string? Snippet { get; set; }

    /// <summary>Subject header of the reply message. Null if not present in metadata.</summary>
    public string? ReplySubject { get; set; }

    /// <summary>Gmail message ID (not thread ID) of the specific reply message.</summary>
    public string? GmailMessageId { get; set; }

    /// <summary>
    /// Non-null when monitoring failed (e.g. insufficient Gmail scope, network error).
    /// The candidature will NOT be marked as having a reply when this is set.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
