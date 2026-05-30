using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Email.DTOs;

public class SaveGoogleClientCredentialsDto
{
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional custom redirect URI.
    /// If not provided, server falls back to GoogleOAuth:RedirectUri from configuration.
    /// </summary>
    public string? RedirectUri { get; set; }
}
