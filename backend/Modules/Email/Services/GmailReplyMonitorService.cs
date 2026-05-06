using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NextStep.Modules.Email.Repositories;
using NextStep.Shared.Config;

namespace NextStep.Modules.Email.Services;

/// <summary>
/// Checks Gmail threads for external replies using the Gmail Threads API (metadata format only).
/// Reuses the same token decryption / refresh pattern as GmailEmailSenderService.
/// Does NOT read full message bodies. Does NOT write, modify, or delete any messages.
/// </summary>
public class GmailReplyMonitorService : IGmailReplyMonitorService
{
    private const string DataProtectionPurpose = "GmailOAuthTokens";
    private const string TokenRefreshUrl       = "https://oauth2.googleapis.com/token";

    private readonly IUserEmailConnectionRepository _connectionRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly GoogleOAuthOptions _oauthOptions;
    private readonly ILogger<GmailReplyMonitorService> _logger;

    public GmailReplyMonitorService(
        IUserEmailConnectionRepository connectionRepo,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<GoogleOAuthOptions> oauthOptions,
        ILogger<GmailReplyMonitorService> logger)
    {
        _connectionRepo    = connectionRepo;
        _httpClientFactory = httpClientFactory;
        _protector         = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _oauthOptions      = oauthOptions.Value;
        _logger            = logger;
    }

    public async Task<ReplyCheckResult> CheckThreadForReplyAsync(
        Guid localUserId,
        string threadId,
        DateTime sentAtUtc,
        CancellationToken ct = default)
    {
        // ── 1. Load Gmail connection ────────────────────────────────────────────
        var connection = await _connectionRepo.GetByUserAndProviderAsync(
            localUserId, "Gmail", ct);

        if (connection is null)
        {
            _logger.LogWarning(
                "GmailReplyMonitor — no Gmail connection found for user {UserId}", localUserId);
            return new ReplyCheckResult
            {
                ErrorMessage = "Gmail account is not connected. Cannot check thread for replies."
            };
        }

        // ── 2. Decrypt tokens ───────────────────────────────────────────────────
        string accessToken;
        string refreshToken;
        try
        {
            accessToken  = _protector.Unprotect(connection.AccessTokenEncrypted);
            refreshToken = _protector.Unprotect(connection.RefreshTokenEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GmailReplyMonitor — failed to decrypt tokens for user {UserId}", localUserId);
            return new ReplyCheckResult
            {
                ErrorMessage = "Failed to decrypt Gmail tokens. Please reconnect your Gmail account."
            };
        }

        // ── 3. Refresh access token if expired ─────────────────────────────────
        if (DateTime.UtcNow >= connection.AccessTokenExpiresAtUtc.AddSeconds(-30))
        {
            var refreshed = await RefreshAccessTokenAsync(refreshToken, connection, ct);
            if (!refreshed.Success)
            {
                _logger.LogWarning(
                    "GmailReplyMonitor — token refresh failed for user {UserId}: {Error}",
                    localUserId, refreshed.ErrorMessage);
                return new ReplyCheckResult { ErrorMessage = refreshed.ErrorMessage };
            }
            accessToken = refreshed.NewAccessToken!;
        }

        // ── 4. Call Gmail Threads API (metadata format) ─────────────────────────
        var threadUrl =
            $"https://gmail.googleapis.com/gmail/v1/users/me/threads/{threadId}?format=metadata";

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(threadUrl, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GmailReplyMonitor — network error fetching thread {ThreadId} for user {UserId}",
                threadId, localUserId);
            return new ReplyCheckResult
            {
                ErrorMessage = $"Network error checking Gmail thread: {ex.Message}"
            };
        }

        // ── 5. Handle Gmail API errors ──────────────────────────────────────────
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);

            if ((int)response.StatusCode == 403)
            {
                _logger.LogWarning(
                    "GmailReplyMonitor — 403 Forbidden for user {UserId} on thread {ThreadId}. " +
                    "User likely needs to reconnect with gmail.readonly scope.",
                    localUserId, threadId);
                return new ReplyCheckResult
                {
                    ErrorMessage =
                        "Insufficient Gmail permissions. Please disconnect and reconnect your Gmail " +
                        "account to grant read access for reply detection."
                };
            }

            _logger.LogWarning(
                "GmailReplyMonitor — Gmail API {Status} for user {UserId}, thread {ThreadId}: {Body}",
                response.StatusCode, localUserId, threadId, errorBody);
            return new ReplyCheckResult
            {
                ErrorMessage = $"Gmail API error {(int)response.StatusCode} checking thread."
            };
        }

        // ── 6. Parse thread messages ────────────────────────────────────────────
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation(
            "GmailReplyMonitor — checking thread {ThreadId} for user {UserId}, sent at {SentAt}",
            threadId, localUserId, sentAtUtc);

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("messages", out var messagesEl) ||
                messagesEl.ValueKind != JsonValueKind.Array)
            {
                return new ReplyCheckResult { HasReply = false };
            }

            var connectedEmail = connection.EmailAddress.ToLowerInvariant();

            foreach (var msg in messagesEl.EnumerateArray())
            {
                // internalDate is Unix epoch in milliseconds
                DateTime? msgDate = null;
                if (msg.TryGetProperty("internalDate", out var dateProp) &&
                    long.TryParse(dateProp.GetString(), out var epochMs))
                {
                    msgDate = DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
                }

                // Only consider messages after the original send time
                if (msgDate is null || msgDate <= sentAtUtc)
                    continue;

                // Extract From header from payload.headers[]
                string? fromHeader = null;
                string? snippet    = null;

                if (msg.TryGetProperty("snippet", out var snippetProp))
                    snippet = snippetProp.GetString();

                if (msg.TryGetProperty("payload", out var payloadEl) &&
                    payloadEl.TryGetProperty("headers", out var headersEl) &&
                    headersEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var header in headersEl.EnumerateArray())
                    {
                        if (header.TryGetProperty("name", out var nameProp) &&
                            nameProp.GetString()?.Equals("From", StringComparison.OrdinalIgnoreCase) == true &&
                            header.TryGetProperty("value", out var valueProp))
                        {
                            fromHeader = valueProp.GetString();
                            break;
                        }
                    }
                }

                // Skip if From is the connected Gmail account (i.e., the user's own sent message)
                if (fromHeader is not null &&
                    fromHeader.Contains(connectedEmail, StringComparison.OrdinalIgnoreCase))
                    continue;

                // A reply was found
                _logger.LogInformation(
                    "GmailReplyMonitor — reply detected in thread {ThreadId} for user {UserId}: " +
                    "from={From}, date={Date}",
                    threadId, localUserId, fromHeader, msgDate);

                return new ReplyCheckResult
                {
                    HasReply     = true,
                    ReplyDateUtc = msgDate,
                    ReplyFrom    = fromHeader,
                    Snippet      = snippet,
                };
            }

            // No reply found
            return new ReplyCheckResult { HasReply = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GmailReplyMonitor — failed to parse thread response for user {UserId}, thread {ThreadId}",
                localUserId, threadId);
            return new ReplyCheckResult
            {
                ErrorMessage = "Failed to parse Gmail thread response."
            };
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private sealed record RefreshResult(bool Success, string? NewAccessToken, string? ErrorMessage);

    private async Task<RefreshResult> RefreshAccessTokenAsync(
        string refreshToken,
        NextStep.Modules.Email.Models.UserEmailConnection connection,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "GmailReplyMonitor — refreshing access token for user {UserId}", connection.UserId);

        using var httpClient = _httpClientFactory.CreateClient();

        var formData = new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["client_id"]     = _oauthOptions.ClientId,
            ["client_secret"] = _oauthOptions.ClientSecret,
            ["refresh_token"] = refreshToken,
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(
                TokenRefreshUrl,
                new FormUrlEncodedContent(formData),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GmailReplyMonitor — network error refreshing token for user {UserId}",
                connection.UserId);
            return new RefreshResult(false, null,
                $"Network error refreshing Gmail token: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GmailReplyMonitor — token refresh failed {Status} for user {UserId}: {Body}",
                response.StatusCode, connection.UserId, body);
            return new RefreshResult(false, null,
                "Failed to refresh Gmail token. Please reconnect your Gmail account.");
        }

        TokenRefreshResponse tokenResponse;
        try
        {
            tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(body)
                ?? throw new InvalidOperationException("Empty token refresh response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailReplyMonitor — could not parse token refresh response");
            return new RefreshResult(false, null, "Could not parse Gmail token refresh response.");
        }

        // Persist refreshed token
        connection.AccessTokenEncrypted    = _protector.Protect(tokenResponse.AccessToken);
        connection.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);
        connection.UpdatedAtUtc            = DateTime.UtcNow;
        await _connectionRepo.UpsertAsync(connection, ct);

        return new RefreshResult(true, tokenResponse.AccessToken, null);
    }

    private sealed class TokenRefreshResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; } = 3600;
    }
}
