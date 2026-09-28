namespace NextStep.Modules.Email.DTOs;

public class GoogleClientCredentialsSummaryDto
{
    public bool HasCredentials { get; set; }
    public string? ClientIdMasked { get; set; }
    public bool UsesCustomRedirectUri { get; set; }
    public string? RedirectUri { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
