using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NextStep.Modules.Email.DTOs;
using NextStep.Modules.Email.Models;
using NextStep.Modules.Email.Repositories;
using NextStep.Shared.Config;

namespace NextStep.Modules.Email.Services;

/// <summary>
/// Manages the Gmail OAuth 2.0 connection lifecycle:
/// generating login URLs, handling callbacks, and checking status.
/// </summary>
public class EmailConnectionService : IEmailConnectionService
{
    private const string DataProtectionPurpose = "GmailOAuthTokens";
    private const string GmailProfileUrl       = "https://gmail.googleapis.com/gmail/v1/users/me/profile";
    private const string TokenExchangeUrl      = "https://oauth2.googleapis.com/token";
    private const string GmailScope =
        "https://www.googleapis.com/auth/gmail.send " +
        "https://www.googleapis.com/auth/gmail.readonly";
    private const string GoogleAuthBase        = "https://accounts.google.com/o/oauth2/v2/auth";
    private static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(10);

    private readonly IOAuthStateRepository _stateRepo;
    private readonly IUserEmailConnectionRepository _connectionRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly GoogleOAuthOptions _options;
    private readonly ILogger<EmailConnectionService> _logger;

    public EmailConnectionService(
        IOAuthStateRepository stateRepo,
        IUserEmailConnectionRepository connectionRepo,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<GoogleOAuthOptions> options,
        ILogger<EmailConnectionService> logger)
    {
        _stateRepo       = stateRepo;
        _connectionRepo  = connectionRepo;
        _httpClientFactory = httpClientFactory;
        _protector       = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _options         = options.Value;
        _logger          = logger;
    }

    // ── GetGoogleLoginUrlAsync ────────────────────────────────────────────────────

    public async Task<string> GetGoogleLoginUrlAsync(
        Guid localUserId,
        CancellationToken ct = default)
    {
        _options.Validate();

        // 1. Generate cryptographically random raw state token (32 bytes → 43 chars base64url)
        var rawStateBytes = RandomNumberGenerator.GetBytes(32);
        var rawState = Base64UrlEncode(rawStateBytes);

        // 2. Hash the raw state — only the hash is stored in the DB
        var stateHash = ComputeSha256Hash(rawState);

        // 3. Persist OAuthState record
        var oauthState = new OAuthState
        {
            UserId         = localUserId,
            Provider       = "Gmail",
            StateTokenHash = stateHash,
            ExpiresAtUtc   = DateTime.UtcNow.Add(StateExpiry),
            Used           = false,
            CreatedAtUtc   = DateTime.UtcNow
        };

        await _stateRepo.AddAsync(oauthState, ct);

        _logger.LogInformation(
            "EmailConnectionService — OAuth state created for user {UserId}, expires {Expiry}",
            localUserId, oauthState.ExpiresAtUtc);

        // 4. Build Google authorization URL
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"]     = _options.ClientId,
            ["redirect_uri"]  = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"]         = GmailScope,
            ["access_type"]   = "offline",
            ["prompt"]        = "consent",
            ["state"]         = rawState,
        };

        var queryString = string.Join("&", queryParams.Select(
            kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"{GoogleAuthBase}?{queryString}";
    }

    // ── HandleGoogleCallbackAsync ─────────────────────────────────────────────────

    public async Task HandleGoogleCallbackAsync(
        string code,
        string state,
        CancellationToken ct = default)
    {
        _options.Validate();

        // 1. Hash the returned state to look it up in DB
        var stateHash = ComputeSha256Hash(state);

        // 2. Find valid (unused, not expired) OAuthState
        var oauthState = await _stateRepo.FindValidAsync("Gmail", stateHash, ct);

        if (oauthState is null)
        {
            _logger.LogWarning(
                "EmailConnectionService — invalid, expired, or already-used OAuth state received.");
            throw new InvalidOperationException(
                "OAuth state is invalid, expired, or has already been used.");
        }

        // 3. Consume state — mark used immediately to prevent replay
        oauthState.Used      = true;
        oauthState.UsedAtUtc = DateTime.UtcNow;
        await _stateRepo.SaveChangesAsync(ct);

        var localUserId = oauthState.UserId;

        // 4. Exchange authorization code for tokens
        using var httpClient = _httpClientFactory.CreateClient();

        var formData = new Dictionary<string, string>
        {
            ["code"]          = code,
            ["client_id"]     = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"]  = _options.RedirectUri,
            ["grant_type"]    = "authorization_code",
        };

        HttpResponseMessage tokenResponse;
        try
        {
            tokenResponse = await httpClient.PostAsync(
                TokenExchangeUrl,
                new FormUrlEncodedContent(formData),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailConnectionService — network error during token exchange");
            throw new InvalidOperationException(
                $"Network error during Gmail OAuth token exchange: {ex.Message}", ex);
        }

        var tokenBody = await tokenResponse.Content.ReadAsStringAsync(ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "EmailConnectionService — token exchange failed {Status}: {Body}",
                tokenResponse.StatusCode, tokenBody);
            throw new InvalidOperationException(
                $"Gmail token exchange failed ({(int)tokenResponse.StatusCode}): {tokenBody}");
        }

        TokenExchangeResponse tokens;
        try
        {
            tokens = JsonSerializer.Deserialize<TokenExchangeResponse>(tokenBody)
                ?? throw new InvalidOperationException("Empty token exchange response.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "EmailConnectionService — failed to parse token response");
            throw new InvalidOperationException("Could not parse Gmail token response.", ex);
        }

        // 5. Fetch Gmail profile to get the email address
        var emailAddress = await FetchGmailEmailAddressAsync(httpClient, tokens.AccessToken, ct);

        // 6. Encrypt tokens and upsert UserEmailConnection
        var connection = new UserEmailConnection
        {
            UserId                   = localUserId,
            Provider                 = "Gmail",
            EmailAddress             = emailAddress,
            AccessTokenEncrypted     = _protector.Protect(tokens.AccessToken),
            RefreshTokenEncrypted    = _protector.Protect(tokens.RefreshToken),
            AccessTokenExpiresAtUtc  = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn - 30),
            CreatedAtUtc             = DateTime.UtcNow
        };

        await _connectionRepo.UpsertAsync(connection, ct);

        _logger.LogInformation(
            "EmailConnectionService — Gmail connection saved for user {UserId}, email: {Email}",
            localUserId, emailAddress);

        // TODO: Implement cleanup of expired OAuthState rows periodically
        // (e.g., via a background IHostedService or scheduled task)
    }

    // ── GetStatusAsync ────────────────────────────────────────────────────────────

    public async Task<EmailConnectionStatusDto> GetStatusAsync(
        Guid localUserId,
        CancellationToken ct = default)
    {
        var connection = await _connectionRepo.GetByUserAndProviderAsync(
            localUserId, "Gmail", ct);

        if (connection is null)
            return new EmailConnectionStatusDto { IsConnected = false, Provider = "Gmail" };

        var isExpired = DateTime.UtcNow >= connection.AccessTokenExpiresAtUtc;

        return new EmailConnectionStatusDto
        {
            IsConnected  = true,
            IsTokenValid = !isExpired, // Basic check: if not expired, we assume it's valid for now
            EmailAddress = connection.EmailAddress,
            Provider     = "Gmail",
            ErrorMessage = isExpired ? "Access token expired. Verification required." : null
        };
    }

    public async Task DisconnectAsync(Guid localUserId, CancellationToken ct = default)
    {
        await _connectionRepo.DeleteAsync(localUserId, "Gmail", ct);
        _logger.LogInformation("EmailConnectionService — Disconnected Gmail for user {UserId}", localUserId);
    }

    public async Task<EmailConnectionStatusDto> VerifyConnectionAsync(Guid localUserId, CancellationToken ct = default)
    {
        var connection = await _connectionRepo.GetByUserAndProviderAsync(
            localUserId, "Gmail", ct);

        if (connection is null)
            return new EmailConnectionStatusDto { IsConnected = false, Provider = "Gmail" };

        string? refreshToken;
        try
        {
            refreshToken = _protector.Unprotect(connection.RefreshTokenEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EmailConnectionService — Failed to decrypt refresh token for user {UserId}", localUserId);
            return new EmailConnectionStatusDto 
            { 
                IsConnected = true, 
                IsTokenValid = false, 
                EmailAddress = connection.EmailAddress,
                ErrorMessage = "Failed to decrypt tokens. Please reconnect."
            };
        }

        // Try to refresh the token to verify it's still valid with Google
        var refreshResult = await RefreshAccessTokenInternalAsync(refreshToken, connection, ct);

        return new EmailConnectionStatusDto
        {
            IsConnected  = true,
            IsTokenValid = refreshResult.Success,
            EmailAddress = connection.EmailAddress,
            Provider     = "Gmail",
            ErrorMessage = refreshResult.ErrorMessage
        };
    }

    private async Task<RefreshResult> RefreshAccessTokenInternalAsync(
        string refreshToken,
        UserEmailConnection connection,
        CancellationToken ct)
    {
        using var httpClient = _httpClientFactory.CreateClient();

        var formData = new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["client_id"]     = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
        };

        try
        {
            var response = await httpClient.PostAsync(TokenExchangeUrl, new FormUrlEncodedContent(formData), ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("EmailConnectionService — Token refresh failed: {Body}", body);
                return new RefreshResult(false, "Gmail account access revoked or expired. Please reconnect.");
            }

            var tokens = JsonSerializer.Deserialize<TokenExchangeResponse>(body);
            if (tokens == null) return new RefreshResult(false, "Invalid response from Google.");

            // Update connection with new access token
            connection.AccessTokenEncrypted    = _protector.Protect(tokens.AccessToken);
            connection.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn - 30);
            connection.UpdatedAtUtc            = DateTime.UtcNow;
            
            await _connectionRepo.UpsertAsync(connection, ct);

            return new RefreshResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailConnectionService — Error during token refresh");
            return new RefreshResult(false, $"Network error: {ex.Message}");
        }
    }

    private record RefreshResult(bool Success, string? ErrorMessage);

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<string> FetchGmailEmailAddressAsync(
        HttpClient httpClient,
        string accessToken,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GmailProfileUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await httpClient.SendAsync(request, ct);
            var body     = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("emailAddress", out var ep))
                    return ep.GetString() ?? string.Empty;
            }

            _logger.LogWarning(
                "EmailConnectionService — could not fetch Gmail profile: {Status} {Body}",
                response.StatusCode, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EmailConnectionService — error fetching Gmail profile");
        }

        return string.Empty;
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    // ── Internal response DTOs ────────────────────────────────────────────────────

    private sealed class TokenExchangeResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; } = 3600;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";
    }
}
