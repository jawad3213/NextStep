using NextStep.Modules.Messaging.Application.Services;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using NextStep.Modules.Messaging.Application.Dtos;
using NextStep.Modules.Messaging.Domain;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Messaging.Infrastructure.Gmail;

/// <summary>
/// Manages the Gmail OAuth 2.0 connection lifecycle: login URL, callback, status, verification.
/// Tokens themselves (refresh, failures) are handled by <see cref="IGmailTokenProvider"/>.
/// </summary>
public class EmailConnectionService : IEmailConnectionService
{
    private const string Provider = "Gmail";
    private const string OAuthClientProtectionPurpose = "GmailOAuthClientCredentials";
    private const string GmailProfileUrl = "https://gmail.googleapis.com/gmail/v1/users/me/profile";
    private const string GoogleAuthBase = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string SendScope = "https://www.googleapis.com/auth/gmail.send";
    public const string ReadScope = "https://www.googleapis.com/auth/gmail.readonly";
    private static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(10);

    private readonly IOAuthStateRepository _stateRepo;
    private readonly IUserEmailConnectionRepository _connectionRepo;
    private readonly IUserOAuthCredentialRepository _oauthCredentialRepo;
    private readonly IGmailTokenProvider _tokens;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _oauthClientProtector;
    private readonly ILogger<EmailConnectionService> _logger;

    public EmailConnectionService(
        IOAuthStateRepository stateRepo,
        IUserEmailConnectionRepository connectionRepo,
        IUserOAuthCredentialRepository oauthCredentialRepo,
        IGmailTokenProvider tokens,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EmailConnectionService> logger)
    {
        _stateRepo = stateRepo;
        _connectionRepo = connectionRepo;
        _oauthCredentialRepo = oauthCredentialRepo;
        _tokens = tokens;
        _httpClientFactory = httpClientFactory;
        _oauthClientProtector = dataProtectionProvider.CreateProtector(OAuthClientProtectionPurpose);
        _logger = logger;
    }

    // ── Custom OAuth client credentials ──────────────────────────────────────────

    public async Task SaveGoogleClientCredentialsAsync(Guid localUserId, SaveGoogleClientCredentialsDto dto, CancellationToken ct = default)
    {
        var clientId = dto.ClientId?.Trim() ?? string.Empty;
        var clientSecret = dto.ClientSecret?.Trim() ?? string.Empty;
        var redirectUri = string.IsNullOrWhiteSpace(dto.RedirectUri) ? null : dto.RedirectUri.Trim();

        if (string.IsNullOrWhiteSpace(clientId))
            throw new BadRequestException("Google Client ID is required.");
        if (!clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("This does not look like a Google Client ID (it should end with .apps.googleusercontent.com).");
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new BadRequestException("Google Client Secret is required.");
        if (redirectUri is not null && (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new BadRequestException("Redirect URI must be a valid http(s) URL.");

        await _oauthCredentialRepo.UpsertAsync(new UserOAuthCredential
        {
            UserId = localUserId,
            Provider = Provider,
            ClientIdEncrypted = _oauthClientProtector.Protect(clientId),
            ClientSecretEncrypted = _oauthClientProtector.Protect(clientSecret),
            RedirectUriOverride = redirectUri,
            CreatedAtUtc = DateTime.UtcNow,
        }, ct);
    }

    public async Task<GoogleClientCredentialsSummaryDto> GetGoogleClientCredentialsSummaryAsync(Guid localUserId, CancellationToken ct = default)
    {
        var credential = await _oauthCredentialRepo.GetByUserAndProviderAsync(localUserId, Provider, ct);
        if (credential is null)
            return new GoogleClientCredentialsSummaryDto { HasCredentials = false, UsesCustomRedirectUri = false };

        string clientId;
        try
        {
            clientId = _oauthClientProtector.Unprotect(credential.ClientIdEncrypted);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Custom Gmail OAuth credentials unreadable for user {UserId}; removing them", localUserId);
            await _oauthCredentialRepo.DeleteAsync(localUserId, Provider, ct);
            return new GoogleClientCredentialsSummaryDto { HasCredentials = false, UsesCustomRedirectUri = false };
        }

        return new GoogleClientCredentialsSummaryDto
        {
            HasCredentials = true,
            ClientIdMasked = MaskClientId(clientId),
            UsesCustomRedirectUri = !string.IsNullOrWhiteSpace(credential.RedirectUriOverride),
            RedirectUri = credential.RedirectUriOverride,
            UpdatedAtUtc = credential.UpdatedAtUtc ?? credential.CreatedAtUtc,
        };
    }

    public Task DeleteGoogleClientCredentialsAsync(Guid localUserId, CancellationToken ct = default) =>
        _oauthCredentialRepo.DeleteAsync(localUserId, Provider, ct);

    // ── OAuth flow ────────────────────────────────────────────────────────────────

    public async Task<string> GetGoogleLoginUrlAsync(Guid localUserId, CancellationToken ct = default)
    {
        GmailOAuthClient client;
        try
        {
            client = await _tokens.ResolveOAuthClientAsync(localUserId, ct);
        }
        catch (GmailConnectionException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        // Expired states are useless (the flow lasts at most StateExpiry): clean them up here.
        try
        {
            await _stateRepo.DeleteExpiredAsync(DateTime.UtcNow.AddHours(-1), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not clean up expired OAuth states");
        }

        // Random state; only its hash is stored, and it can be used once within StateExpiry.
        var rawState = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await _stateRepo.AddAsync(new OAuthState
        {
            UserId = localUserId,
            Provider = Provider,
            StateTokenHash = ComputeSha256Hash(rawState),
            ExpiresAtUtc = DateTime.UtcNow.Add(StateExpiry),
            Used = false,
            CreatedAtUtc = DateTime.UtcNow,
        }, ct);

        var query = new Dictionary<string, string>
        {
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = client.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = $"{SendScope} {ReadScope}",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = rawState,
        };
        return $"{GoogleAuthBase}?{string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"))}";
    }

    /// <summary>Completes the OAuth flow. Throws <see cref="GmailConnectionException"/> with a user-safe message.</summary>
    public async Task HandleGoogleCallbackAsync(string code, string state, CancellationToken ct = default)
    {
        var oauthState = await _stateRepo.FindValidAsync(Provider, ComputeSha256Hash(state), ct);
        if (oauthState is null)
        {
            _logger.LogWarning("Gmail OAuth callback with an invalid, expired or already-used state");
            throw new GmailConnectionException("This Gmail connection link has expired or was already used. Please click \"Connect Gmail\" again.");
        }

        // Consumed before anything else: a state can never be replayed.
        oauthState.Used = true;
        oauthState.UsedAtUtc = DateTime.UtcNow;
        await _stateRepo.SaveChangesAsync(ct);

        var localUserId = oauthState.UserId;
        var client = await _tokens.ResolveOAuthClientAsync(localUserId, ct);

        using var http = _httpClientFactory.CreateClient();
        HttpResponseMessage response;
        string body;
        try
        {
            response = await http.PostAsync(GmailTokenProvider.TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = client.ClientId,
                ["client_secret"] = client.ClientSecret,
                ["redirect_uri"] = client.RedirectUri,
                ["grant_type"] = "authorization_code",
            }), ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Gmail OAuth token exchange unreachable");
            throw new GmailConnectionException("Google could not be reached to finish the connection. Please try again.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = GmailTokenProvider.OAuthErrorCode(body);
            _logger.LogWarning("Gmail OAuth token exchange failed: {Status} {Error}", (int)response.StatusCode, error);
            throw new GmailConnectionException(error switch
            {
                "invalid_grant" => "The Google authorization expired before it could be used. Please connect again.",
                "redirect_uri_mismatch" => "Google rejected the redirect URI. It must match exactly the one registered in your Google Cloud OAuth client.",
                "invalid_client" or "unauthorized_client" => GmailMessages.ClientRejected,
                _ => "Google refused the connection. Please try again.",
            });
        }

        var tokens = GmailTokenProvider.ParseTokens(body);
        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
            throw new GmailConnectionException("Google returned an unexpected answer. Please try again.");

        // With granular consent the user can untick permissions: without them nothing would work later.
        var granted = (tokens.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!granted.Contains(SendScope) || !granted.Contains(ReadScope))
        {
            _logger.LogWarning("Gmail OAuth for user {UserId} granted only: {Scopes}", localUserId, tokens.Scope);
            throw new GmailConnectionException(
                "NextStep needs permission to send and read your emails. Connect again and tick both Gmail permissions on Google's screen.");
        }

        var existing = await _connectionRepo.GetByUserAndProviderAsync(localUserId, Provider, ct);
        var refreshTokenEncrypted = !string.IsNullOrWhiteSpace(tokens.RefreshToken)
            ? _tokens.Protect(tokens.RefreshToken)
            : existing?.RefreshTokenEncrypted;
        if (string.IsNullOrWhiteSpace(refreshTokenEncrypted))
        {
            // Google only sends a refresh token on the first consent for a client.
            throw new GmailConnectionException(
                "Google did not provide long-term access. Remove NextStep from https://myaccount.google.com/permissions, then connect again.");
        }

        var emailAddress = await FetchGmailEmailAddressAsync(http, tokens.AccessToken, ct);
        if (string.IsNullOrWhiteSpace(emailAddress))
            throw new GmailConnectionException("Your Gmail address could not be read. Please try connecting again.");

        await _connectionRepo.UpsertAsync(new UserEmailConnection
        {
            UserId = localUserId,
            Provider = Provider,
            EmailAddress = emailAddress,
            AccessTokenEncrypted = _tokens.Protect(tokens.AccessToken),
            RefreshTokenEncrypted = refreshTokenEncrypted,
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(tokens.ExpiresIn, 60)),
            ReconnectReason = null,
            ReconnectRequiredAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow,
        }, ct);

        _logger.LogInformation("Gmail connected for user {UserId}", localUserId);
    }

    // ── Status ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stored status, without calling Google. An expired access token is normal (it is renewed
    /// automatically when needed): only a recorded reconnect reason marks the connection unusable.
    /// </summary>
    public async Task<EmailConnectionStatusDto> GetStatusAsync(Guid localUserId, CancellationToken ct = default)
    {
        var hasCustomCredentials = (await GetGoogleClientCredentialsSummaryAsync(localUserId, ct)).HasCredentials;
        var connection = await _connectionRepo.GetByUserAndProviderAsync(localUserId, Provider, ct);
        return ToStatus(connection, hasCustomCredentials, connection?.ReconnectReason);
    }

    public async Task DisconnectAsync(Guid localUserId, CancellationToken ct = default)
    {
        await _connectionRepo.DeleteAsync(localUserId, Provider, ct);
        _logger.LogInformation("Gmail disconnected for user {UserId}", localUserId);
    }

    /// <summary>Checks the connection with Google now (forces a token refresh).</summary>
    public async Task<EmailConnectionStatusDto> VerifyConnectionAsync(Guid localUserId, CancellationToken ct = default)
    {
        var hasCustomCredentials = (await GetGoogleClientCredentialsSummaryAsync(localUserId, ct)).HasCredentials;
        var access = await _tokens.GetAccessAsync(localUserId, forceRefresh: true, ct);
        if (access.Failure == GmailFailure.NotConnected)
            return ToStatus(null, hasCustomCredentials, null);
        return ToStatus(access.Connection, hasCustomCredentials, access.Success ? null : access.UserMessage, access.Failure == GmailFailure.Transient);
    }

    private static EmailConnectionStatusDto ToStatus(UserEmailConnection? connection, bool hasCustomCredentials, string? problem, bool transient = false) =>
        connection is null
            ? new EmailConnectionStatusDto { IsConnected = false, IsTokenValid = false, Provider = Provider, HasCustomClientCredentials = hasCustomCredentials }
            : new EmailConnectionStatusDto
            {
                IsConnected = true,
                IsTokenValid = problem is null,
                NeedsReconnect = problem is not null && !transient,
                EmailAddress = connection.EmailAddress,
                Provider = Provider,
                HasCustomClientCredentials = hasCustomCredentials,
                ErrorMessage = problem,
            };

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string? MaskClientId(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? null
        : clientId.Length <= 8 ? "****"
        : $"{clientId[..4]}...{clientId[^4..]}";

    private async Task<string> FetchGmailEmailAddressAsync(HttpClient http, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GmailProfileUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("emailAddress", out var address))
                    return address.GetString() ?? string.Empty;
            }
            _logger.LogWarning("Gmail profile could not be read: {Status}", (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Gmail profile request failed");
        }
        return string.Empty;
    }

    private static string ComputeSha256Hash(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
