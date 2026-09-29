namespace NextStep.Modules.Messaging.Application.Dtos;

/// <summary>
/// Represents the Gmail OAuth connection status for the current user.
/// </summary>
public class EmailConnectionStatusDto
{
    public bool IsConnected { get; set; }
    public bool IsTokenValid { get; set; } = true;
    public bool HasCustomClientCredentials { get; set; }

    /// <summary>The user must reconnect Gmail (access revoked, permission missing...). False for temporary problems.</summary>
    public bool NeedsReconnect { get; set; }

    /// <summary>User-facing explanation when the connection cannot be used.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The Gmail address connected, if any.</summary>
    public string? EmailAddress { get; set; }

    public string Provider { get; set; } = "Gmail";
}
