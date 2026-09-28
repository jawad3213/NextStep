namespace NextStep.Modules.Email.Models;

/// <summary>
/// Stores per-user OAuth client credentials (BYO app) encrypted at rest.
/// This allows each user/workspace to connect Gmail with their own client ID/secret.
/// </summary>
public class UserOAuthCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Local DB user id (utilisateur.id_utilisateur).</summary>
    public Guid UserId { get; set; }

    /// <summary>Provider identifier, e.g. "Gmail".</summary>
    public string Provider { get; set; } = "Gmail";

    /// <summary>Encrypted OAuth client ID.</summary>
    public string ClientIdEncrypted { get; set; } = string.Empty;

    /// <summary>Encrypted OAuth client secret.</summary>
    public string ClientSecretEncrypted { get; set; } = string.Empty;

    /// <summary>
    /// Optional per-user redirect URI override.
    /// If empty, global GoogleOAuth:RedirectUri is used.
    /// </summary>
    public string? RedirectUriOverride { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
