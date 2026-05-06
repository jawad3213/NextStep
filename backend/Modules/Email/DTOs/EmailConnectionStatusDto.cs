namespace NextStep.Modules.Email.DTOs;

/// <summary>
/// Represents the Gmail OAuth connection status for the current user.
/// </summary>
public class EmailConnectionStatusDto
{
    public bool IsConnected { get; set; }
    public bool IsTokenValid { get; set; } = true;
    public string? ErrorMessage { get; set; }

    /// <summary>The Gmail address connected, if any.</summary>
    public string? EmailAddress { get; set; }

    public string Provider { get; set; } = "Gmail";
}
