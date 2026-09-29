using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NextStep.Modules.Messaging.Domain;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Shared.Config;

namespace NextStep.Modules.Messaging.Infrastructure.Gmail;

/// <summary>Why Gmail cannot be used right now. Only <see cref="Transient"/> is worth retrying as-is.</summary>
public enum GmailFailure
{
    None,
    NotConnected,
    /// <summary>Tokens cannot be decrypted (server keys changed): reconnect.</summary>
    Unreadable,
    /// <summary>Google refused the refresh token (revoked, expired, password changed): reconnect.</summary>
    Revoked,
    /// <summary>The user did not grant the send/read permissions: reconnect and allow them.</summary>
    MissingPermission,
    /// <summary>Google rejected the OAuth client (Client ID/Secret): fix the credentials.</summary>
    ClientRejected,
    /// <summary>Network error, Google 5xx or rate limit: try again later.</summary>
    Transient,
}

public sealed record GmailAccess(UserEmailConnection? Connection, string? AccessToken, GmailFailure Failure, string? UserMessage)
{
    public bool Success => AccessToken is not null;

    /// <summary>The user has to act in Gmail settings (reconnect or fix credentials).</summary>
    public bool NeedsUserAction => Failure is GmailFailure.NotConnected or GmailFailure.Unreadable or GmailFailure.Revoked
        or GmailFailure.MissingPermission or GmailFailure.ClientRejected;
}

public sealed record GmailOAuthClient(string ClientId, string ClientSecret, string RedirectUri);

/// <summary>User-facing messages: never include provider response bodies or exception texts.</summary>
public static class GmailMessages
{
    public const string NotConnected = "Gmail is not connected. Connect your Gmail account in Settings > Gmail.";
    public const string Unreadable = "Your Gmail connection can no longer be read. Please reconnect your Gmail account.";
    public const string Revoked = "Gmail access was revoked or has expired. Please reconnect your Gmail account.";
    public const string MissingPermission =
        "NextStep does not have permission to send and read your emails. Reconnect Gmail and allow all requested permissions.";
    public const string ClientRejected =
        "Google rejected the OAuth client (Client ID / Client Secret). Check the credentials in Settings > Gmail.";
    public const string Transient = "Gmail is temporarily unreachable. Please try again in a moment.";
    public const string RateLimited = "Gmail's sending limit was reached. Please try again later.";

    public static string For(GmailFailure failure) => failure switch
    {
        GmailFailure.NotConnected => NotConnected,
        GmailFailure.Unreadable => Unreadable,
        GmailFailure.Revoked => Revoked,
        GmailFailure.MissingPermission => MissingPermission,
        GmailFailure.ClientRejected => ClientRejected,
        _ => Transient,
    };
}

public interface IGmailTokenProvider
{
    /// <summary>
    /// A usable access token for the user's Gmail connection, refreshed when expired (or when
    /// <paramref name="forceRefresh"/>). Failures that need the user are recorded on the connection.
    /// Never throws for provider failures.
    /// </summary>
    /// <param name="skipIfReconnectRequired">Background work: do not call Google for a connection already known to need reconnecting.</param>
    Task<GmailAccess> GetAccessAsync(Guid localUserId, bool forceRefresh = false, CancellationToken ct = default, bool skipIfReconnectRequired = false);

    /// <summary>Records that the user must reconnect (e.g. Gmail answered 401/403 with a fresh token).</summary>
    Task MarkNeedsUserActionAsync(UserEmailConnection connection, GmailFailure failure, CancellationToken ct = default);

    /// <summary>The OAuth client used for this user: their own credentials, else the server's.</summary>
    Task<GmailOAuthClient> ResolveOAuthClientAsync(Guid localUserId, CancellationToken ct = default);

    string Protect(string token);
}

/// <summary>Single place for Gmail OAuth tokens: decrypt, refresh, classify failures, persist.</summary>
public class GmailTokenProvider(
    IUserEmailConnectionRepository connections,
    IUserOAuthCredentialRepository credentials,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtection,
    IOptions<GoogleOAuthOptions> options,
    ILogger<GmailTokenProvider> logger) : IGmailTokenProvider
{
    public const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string Provider = "Gmail";
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

    private readonly IDataProtector _tokenProtector = dataProtection.CreateProtector("GmailOAuthTokens");
    private readonly IDataProtector _clientProtector = dataProtection.CreateProtector("GmailOAuthClientCredentials");
    private readonly GoogleOAuthOptions _options = options.Value;

    public string Protect(string token) => _tokenProtector.Protect(token);

    public async Task<GmailAccess> GetAccessAsync(Guid localUserId, bool forceRefresh = false, CancellationToken ct = default, bool skipIfReconnectRequired = false)
    {
        var connection = await connections.GetByUserAndProviderAsync(localUserId, Provider, ct);
        if (connection is null)
            return Fail(null, GmailFailure.NotConnected);
        if (skipIfReconnectRequired && connection.ReconnectReason is not null)
            return new GmailAccess(connection, null, GmailFailure.Revoked, connection.ReconnectReason);

        string accessToken, refreshToken;
        try
        {
            accessToken = _tokenProtector.Unprotect(connection.AccessTokenEncrypted);
            refreshToken = _tokenProtector.Unprotect(connection.RefreshTokenEncrypted);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Gmail tokens unreadable for user {UserId}", localUserId);
            await MarkNeedsUserActionAsync(connection, GmailFailure.Unreadable, ct);
            return Fail(connection, GmailFailure.Unreadable);
        }

        if (!forceRefresh && DateTime.UtcNow < connection.AccessTokenExpiresAtUtc - ExpiryMargin)
            return new GmailAccess(connection, accessToken, GmailFailure.None, null);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            await MarkNeedsUserActionAsync(connection, GmailFailure.Revoked, ct);
            return Fail(connection, GmailFailure.Revoked);
        }

        return await RefreshAsync(connection, refreshToken, ct);
    }

    private async Task<GmailAccess> RefreshAsync(UserEmailConnection connection, string refreshToken, CancellationToken ct)
    {
        GmailOAuthClient client;
        try
        {
            client = await ResolveOAuthClientAsync(connection.UserId, ct);
        }
        catch (GmailConnectionException ex)
        {
            logger.LogWarning(ex, "Gmail OAuth client unavailable for user {UserId}", connection.UserId);
            return Fail(connection, GmailFailure.ClientRejected);
        }

        HttpResponseMessage response;
        string body;
        try
        {
            using var http = httpClientFactory.CreateClient();
            response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = client.ClientId,
                ["client_secret"] = client.ClientSecret,
                ["refresh_token"] = refreshToken,
            }), ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Gmail token refresh unreachable for user {UserId}", connection.UserId);
            return Fail(connection, GmailFailure.Transient);
        }

        if (!response.IsSuccessStatusCode)
        {
            var failure = ClassifyTokenError(response.StatusCode, body);
            logger.LogWarning("Gmail token refresh failed for user {UserId}: {Status} {Error}",
                connection.UserId, (int)response.StatusCode, OAuthErrorCode(body));
            if (failure != GmailFailure.Transient)
                await MarkNeedsUserActionAsync(connection, failure, ct);
            return Fail(connection, failure);
        }

        var tokens = ParseTokens(body);
        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            logger.LogWarning("Gmail token refresh returned an unreadable body for user {UserId}", connection.UserId);
            return Fail(connection, GmailFailure.Transient);
        }

        connection.AccessTokenEncrypted = _tokenProtector.Protect(tokens.AccessToken);
        // Google may rotate the refresh token; keep the old one when it doesn't.
        if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
            connection.RefreshTokenEncrypted = _tokenProtector.Protect(tokens.RefreshToken);
        connection.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(tokens.ExpiresIn, 60));
        connection.ReconnectReason = null;
        connection.ReconnectRequiredAtUtc = null;
        await connections.UpsertAsync(connection, ct);

        return new GmailAccess(connection, tokens.AccessToken, GmailFailure.None, null);
    }

    public async Task MarkNeedsUserActionAsync(UserEmailConnection connection, GmailFailure failure, CancellationToken ct = default)
    {
        var reason = GmailMessages.For(failure);
        if (connection.ReconnectReason == reason) return;
        connection.ReconnectReason = reason;
        connection.ReconnectRequiredAtUtc = DateTime.UtcNow;
        await connections.UpsertAsync(connection, ct);
    }

    public async Task<GmailOAuthClient> ResolveOAuthClientAsync(Guid localUserId, CancellationToken ct = default)
    {
        var custom = await credentials.GetByUserAndProviderAsync(localUserId, Provider, ct);
        if (custom is not null)
        {
            string clientId, clientSecret;
            try
            {
                clientId = _clientProtector.Unprotect(custom.ClientIdEncrypted);
                clientSecret = _clientProtector.Unprotect(custom.ClientSecretEncrypted);
            }
            catch (CryptographicException ex)
            {
                // Only unreadable (not merely invalid) credentials are removed. No silent fallback to
                // the server's client: tokens issued to the user's client would not work with it.
                logger.LogWarning(ex, "Custom Gmail OAuth credentials unreadable for user {UserId}; removing them", localUserId);
                await credentials.DeleteAsync(localUserId, Provider, ct);
                throw new GmailConnectionException(
                    "Your saved Google OAuth credentials can no longer be read. Enter your Client ID and Client Secret again.");
            }

            var redirectUri = string.IsNullOrWhiteSpace(custom.RedirectUriOverride) ? _options.RedirectUri : custom.RedirectUriOverride.Trim();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                throw new GmailConnectionException("Your saved Google OAuth credentials are incomplete. Enter them again in Settings > Gmail.");
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
                throw new GmailConnectionException("The Gmail redirect URI is not a valid URL. Check it in Settings > Gmail.");
            return new GmailOAuthClient(clientId, clientSecret, redirectUri);
        }

        try
        {
            _options.Validate();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Server Google OAuth configuration is incomplete");
            throw new GmailConnectionException(
                "Gmail connection is not configured on this server. Add your own Google OAuth credentials in Settings > Gmail.");
        }
        return new GmailOAuthClient(_options.ClientId, _options.ClientSecret, _options.RedirectUri);
    }

    /// <summary>Maps a failed Google token endpoint answer (RFC 6749 error codes) to a failure kind.</summary>
    public static GmailFailure ClassifyTokenError(HttpStatusCode status, string body)
    {
        if ((int)status >= 500 || status == HttpStatusCode.TooManyRequests)
            return GmailFailure.Transient;
        return OAuthErrorCode(body) switch
        {
            "invalid_client" or "unauthorized_client" => GmailFailure.ClientRejected,
            "invalid_scope" => GmailFailure.MissingPermission,
            // invalid_grant: refresh token revoked/expired; anything else 4xx also needs a new consent.
            _ => GmailFailure.Revoked,
        };
    }

    /// <summary>The "error" field of a Google OAuth error body (never the whole body).</summary>
    public static string? OAuthErrorCode(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static TokenResponse? ParseTokens(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<TokenResponse>(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static GmailAccess Fail(UserEmailConnection? connection, GmailFailure failure) =>
        new(connection, null, failure, GmailMessages.For(failure));

    internal sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; } = 3600;
        [JsonPropertyName("scope")] public string? Scope { get; set; }
    }
}

/// <summary>A Gmail connection problem whose message is safe to show to the user.</summary>
public class GmailConnectionException(string userMessage, Exception? inner = null) : Exception(userMessage, inner);

/// <summary>Maps Gmail API (send / read) error responses to failure kinds and safe messages.</summary>
public static class GmailApiErrors
{
    public static (GmailFailure Failure, string Message) Describe(HttpStatusCode status, string body)
    {
        var reason = ApiReason(body);
        return (int)status switch
        {
            401 => (GmailFailure.Revoked, GmailMessages.Revoked),
            403 when reason is "rateLimitExceeded" or "userRateLimitExceeded" or "dailyLimitExceeded"
                => (GmailFailure.Transient, GmailMessages.RateLimited),
            403 => (GmailFailure.MissingPermission, GmailMessages.MissingPermission),
            429 => (GmailFailure.Transient, GmailMessages.RateLimited),
            400 => (GmailFailure.None, "Gmail rejected the email. Check the recipient address and try again."),
            404 => (GmailFailure.None, "The Gmail message or thread no longer exists."),
            >= 500 => (GmailFailure.Transient, GmailMessages.Transient),
            _ => (GmailFailure.None, $"Gmail returned an unexpected error ({(int)status}). Please try again."),
        };
    }

    private static string? ApiReason(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in errors.EnumerateArray())
                    if (item.TryGetProperty("reason", out var r)) return r.GetString();
            }
        }
        catch (JsonException) { }
        return null;
    }
}
