using NextStep.Modules.Messaging.Infrastructure.Gmail;
namespace NextStep.Modules.Messaging.Application.Services;

/// <summary>
/// Result of an email send attempt through a provider (Gmail).
/// </summary>
public class SendEmailResult
{
    public bool Success { get; set; }

    /// <summary>Gmail message ID if sending succeeded. Null on failure.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Gmail thread ID if sending succeeded. Null on failure.</summary>
    public string? ProviderThreadId { get; set; }

    /// <summary>Clear error description if sending failed. Null on success.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The user must reconnect Gmail before sending can work.</summary>
    public bool NeedsReconnect { get; set; }
}
