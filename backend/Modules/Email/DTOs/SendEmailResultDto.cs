namespace NextStep.Modules.Email.DTOs;

/// <summary>
/// Result DTO returned by the send draft endpoint.
/// </summary>
public class SendEmailResultDto
{
    public bool Success { get; set; }

    public Guid DraftId { get; set; }

    /// <summary>Gmail message ID returned by the Gmail API if sending succeeded.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Clear error message if sending failed.</summary>
    public string? ErrorMessage { get; set; }

    public DateTime? SentAtUtc { get; set; }
}
