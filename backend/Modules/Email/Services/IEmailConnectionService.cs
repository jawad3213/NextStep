using NextStep.Modules.Email.DTOs;

namespace NextStep.Modules.Email.Services;

/// <summary>
/// Service for managing Gmail OAuth connections: login, callback, and status.
/// </summary>
public interface IEmailConnectionService
{
    /// <summary>
    /// Generates the Google OAuth authorization URL for the given local user.
    /// Creates a short-lived, single-use OAuthState record in the database.
    /// </summary>
    Task<string> GetGoogleLoginUrlAsync(Guid localUserId, CancellationToken ct = default);

    /// <summary>
    /// Handles the Google OAuth callback.
    /// Validates state (hash check, expiry, single-use), exchanges the code for tokens,
    /// fetches the Gmail email address, and upserts UserEmailConnection with encrypted tokens.
    /// </summary>
    Task HandleGoogleCallbackAsync(string code, string state, CancellationToken ct = default);

    /// <summary>
    /// Returns the current Gmail connection status for the given local user.
    /// </summary>
    Task<EmailConnectionStatusDto> GetStatusAsync(Guid localUserId, CancellationToken ct = default);

    /// <summary>
    /// Forcefully deletes the user's Gmail connection from the database.
    /// </summary>
    Task DisconnectAsync(Guid localUserId, CancellationToken ct = default);

    /// <summary>
    /// Deep-verifies the Gmail connection by trying to refresh the access token.
    /// Returns the updated status.
    /// </summary>
    Task<EmailConnectionStatusDto> VerifyConnectionAsync(Guid localUserId, CancellationToken ct = default);

    Task SaveGoogleClientCredentialsAsync(
        Guid localUserId,
        SaveGoogleClientCredentialsDto dto,
        CancellationToken ct = default);

    Task<GoogleClientCredentialsSummaryDto> GetGoogleClientCredentialsSummaryAsync(
        Guid localUserId,
        CancellationToken ct = default);

    Task DeleteGoogleClientCredentialsAsync(
        Guid localUserId,
        CancellationToken ct = default);
}
