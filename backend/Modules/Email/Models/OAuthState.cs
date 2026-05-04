namespace NextStep.Modules.Email.Models;

/// <summary>
/// Short-lived, single-use CSRF-protection record for OAuth flows.
/// The raw state token is sent to Google. Only a SHA-256 hash is stored in the DB.
/// </summary>
public class OAuthState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Local DB user id who initiated the OAuth flow.</summary>
    public Guid UserId { get; set; }

    /// <summary>Provider identifier, e.g. "Gmail".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the raw state token sent to Google.
    /// Do not store the raw token here.
    /// </summary>
    public string StateTokenHash { get; set; } = string.Empty;

    /// <summary>UTC expiry — states expire after a short window (10 minutes).</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>True once the callback has consumed this state record.</summary>
    public bool Used { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAtUtc { get; set; }
}
