namespace NextStep.Modules.Messaging.Application.Dtos;

/// <summary>
/// Result DTO returned by the send draft endpoint.
/// </summary>
public class SendEmailResultDto
{
    public bool Success { get; set; }

    public Guid DraftId { get; set; }

    /// <summary>Gmail message ID returned by the Gmail API if sending succeeded.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Gmail thread ID returned by the Gmail API if sending succeeded.</summary>
    public string? ProviderThreadId { get; set; }

    /// <summary>Clear error message if sending failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Sending failed because the user must reconnect Gmail (show a link to Settings > Gmail).</summary>
    public bool NeedsReconnect { get; set; }

    public DateTime? SentAtUtc { get; set; }
}
