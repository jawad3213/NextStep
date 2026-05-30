namespace NextStep.Shared.Config;

/// <summary>
/// Configuration options for Google OAuth 2.0.
/// Bind from appsettings.json section "GoogleOAuth" or environment variables
/// GoogleOAuth__ClientId / GoogleOAuth__ClientSecret / GoogleOAuth__RedirectUri.
/// </summary>
public class GoogleOAuthOptions
{
    public const string SectionName = "GoogleOAuth";

    /// <summary>Google OAuth 2.0 Client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Google OAuth 2.0 Client Secret. Never expose in frontend or API responses.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Redirect URI registered in Google Cloud Console.
    /// Must be browser-accessible, e.g. http://localhost:5000/api/email-connections/google/callback.
    /// Do NOT use Docker-internal service names here.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Validates that all required values are set.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("GoogleOAuth:ClientId is not configured.");
        if (string.IsNullOrWhiteSpace(ClientSecret))
            throw new InvalidOperationException("GoogleOAuth:ClientSecret is not configured.");
        if (string.IsNullOrWhiteSpace(RedirectUri))
            throw new InvalidOperationException("GoogleOAuth:RedirectUri is not configured.");
    }
}
