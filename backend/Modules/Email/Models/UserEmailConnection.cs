namespace NextStep.Modules.Email.Models;

/// <summary>
/// Stores encrypted Gmail OAuth tokens per user.
/// Tokens are encrypted at rest using ASP.NET Core Data Protection.
/// </summary>
public class UserEmailConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Local DB user id (utilisateur.id_utilisateur).</summary>
    public Guid UserId { get; set; }

    /// <summary>Provider identifier, e.g. "Gmail".</summary>
    public string Provider { get; set; } = "Gmail";

    /// <summary>Gmail address associated with the connected account.</summary>
    public string EmailAddress { get; set; } = string.Empty;

    /// <summary>
    /// Access token encrypted with ASP.NET Core Data Protection.
    /// Never expose this value through API responses.
    /// </summary>
    public string AccessTokenEncrypted { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token encrypted with ASP.NET Core Data Protection.
    /// Never expose this value through API responses.
    /// </summary>
    public string RefreshTokenEncrypted { get; set; } = string.Empty;

    /// <summary>UTC datetime when the access token expires.</summary>
    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
