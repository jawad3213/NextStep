using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NextStep.Modules.Email.Models;
using NextStep.Modules.Email.Repositories;
using NextStep.Shared.Config;

namespace NextStep.Modules.Email.Services;

/// <summary>
/// Checks Gmail threads for recruiter replies.
/// Uses encrypted OAuth tokens and supports per-user BYO OAuth credentials.
/// </summary>
public class GmailReplyMonitorService : IGmailReplyMonitorService
{
    private const string TokenProtectionPurpose = "GmailOAuthTokens";
    private const string OAuthClientProtectionPurpose = "GmailOAuthClientCredentials";
    private const string TokenRefreshUrl = "https://oauth2.googleapis.com/token";

    private readonly IUserEmailConnectionRepository _connectionRepo;
    private readonly IUserOAuthCredentialRepository _oauthCredentialRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _tokenProtector;
    private readonly IDataProtector _oauthClientProtector;
    private readonly GoogleOAuthOptions _oauthOptions;
    private readonly ILogger<GmailReplyMonitorService> _logger;

    public GmailReplyMonitorService(
        IUserEmailConnectionRepository connectionRepo,
        IUserOAuthCredentialRepository oauthCredentialRepo,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<GoogleOAuthOptions> oauthOptions,
        ILogger<GmailReplyMonitorService> logger)
    {
        _connectionRepo = connectionRepo;
        _oauthCredentialRepo = oauthCredentialRepo;
        _httpClientFactory = httpClientFactory;
        _tokenProtector = dataProtectionProvider.CreateProtector(TokenProtectionPurpose);
        _oauthClientProtector = dataProtectionProvider.CreateProtector(OAuthClientProtectionPurpose);
        _oauthOptions = oauthOptions.Value;
        _logger = logger;
    }

    public async Task<ReplyCheckResult> CheckThreadForReplyAsync(
        Guid localUserId,
        string threadId,
        DateTime sentAtUtc,
        CancellationToken ct = default)
    {
        var connection = await _connectionRepo.GetByUserAndProviderAsync(localUserId, "Gmail", ct);
        if (connection is null)
        {
            return new ReplyCheckResult
            {
                ErrorMessage = "Gmail account is not connected. Cannot check thread for replies."
            };
        }

        string accessToken;
        string refreshToken;
        try
        {
            accessToken = _tokenProtector.Unprotect(connection.AccessTokenEncrypted);
            refreshToken = _tokenProtector.Unprotect(connection.RefreshTokenEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailReplyMonitor - failed to decrypt tokens for user {UserId}", localUserId);
            return new ReplyCheckResult
            {
                ErrorMessage = "Failed to decrypt Gmail tokens. Please reconnect your Gmail account."
            };
        }

        if (DateTime.UtcNow >= connection.AccessTokenExpiresAtUtc.AddSeconds(-30))
        {
            var refreshed = await RefreshAccessTokenAsync(refreshToken, connection, ct);
            if (!refreshed.Success)
            {
                return new ReplyCheckResult { ErrorMessage = refreshed.ErrorMessage };
            }
            accessToken = refreshed.NewAccessToken!;
        }

        var threadUrl = $"https://gmail.googleapis.com/gmail/v1/users/me/threads/{threadId}?format=full";
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(threadUrl, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailReplyMonitor - network error fetching thread {ThreadId} for user {UserId}", threadId, localUserId);
            return new ReplyCheckResult { ErrorMessage = $"Network error checking Gmail thread: {ex.Message}" };
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            if ((int)response.StatusCode == 403)
            {
                return new ReplyCheckResult
                {
                    ErrorMessage = "Insufficient Gmail permissions. Please disconnect and reconnect your Gmail account."
                };
            }

            _logger.LogWarning(
                "GmailReplyMonitor - Gmail API {Status} for user {UserId}, thread {ThreadId}: {Body}",
                response.StatusCode, localUserId, threadId, errorBody);
            return new ReplyCheckResult
            {
                ErrorMessage = $"Gmail API error {(int)response.StatusCode} checking thread."
            };
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("messages", out var messagesEl) || messagesEl.ValueKind != JsonValueKind.Array)
                return new ReplyCheckResult { HasReply = false };

            var connectedEmail = connection.EmailAddress.ToLowerInvariant();
            DateTime? latestReplyDate = null;
            string? latestReplyFrom = null;
            string? latestReplySubject = null;
            string? latestReplySnippet = null;
            string? latestGmailMessageId = null;

            foreach (var msg in messagesEl.EnumerateArray())
            {
                DateTime? msgDate = null;
                if (msg.TryGetProperty("internalDate", out var dateProp) &&
                    long.TryParse(dateProp.GetString(), out var epochMs))
                {
                    msgDate = DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
                }

                if (msgDate is null || msgDate <= sentAtUtc)
                    continue;

                string? fromHeader = null;
                string? subjectHeader = null;
                string? snippet = null;
                string? gmailMsgId = null;
                string? messageText = null;

                if (msg.TryGetProperty("id", out var msgIdProp))
                    gmailMsgId = msgIdProp.GetString();
                if (msg.TryGetProperty("snippet", out var snippetProp))
                    snippet = snippetProp.GetString();

                if (msg.TryGetProperty("payload", out var payloadEl) &&
                    payloadEl.TryGetProperty("headers", out var headersEl) &&
                    headersEl.ValueKind == JsonValueKind.Array)
                {
                    messageText = ExtractReadableMessageText(payloadEl);

                    foreach (var header in headersEl.EnumerateArray())
                    {
                        if (!header.TryGetProperty("name", out var nameProp) ||
                            !header.TryGetProperty("value", out var valueProp))
                            continue;

                        var hName = nameProp.GetString();
                        if (hName?.Equals("From", StringComparison.OrdinalIgnoreCase) == true)
                            fromHeader = valueProp.GetString();
                        else if (hName?.Equals("Subject", StringComparison.OrdinalIgnoreCase) == true)
                            subjectHeader = valueProp.GetString();

                        if (fromHeader is not null && subjectHeader is not null)
                            break;
                    }
                }

                if (fromHeader is not null &&
                    fromHeader.Contains(connectedEmail, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (latestReplyDate is null || msgDate > latestReplyDate)
                {
                    latestReplyDate = msgDate;
                    latestReplyFrom = fromHeader;
                    latestReplySubject = subjectHeader;
                    latestReplySnippet = BuildClassificationText(snippet, messageText);
                    latestGmailMessageId = gmailMsgId;
                }
            }

            if (latestReplyDate is null)
            {
                return new ReplyCheckResult { HasReply = false };
            }

            return new ReplyCheckResult
            {
                HasReply = true,
                ReplyDateUtc = latestReplyDate,
                ReplyFrom = latestReplyFrom,
                Snippet = latestReplySnippet,
                ReplySubject = latestReplySubject,
                GmailMessageId = latestGmailMessageId,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailReplyMonitor - failed to parse thread response for user {UserId}, thread {ThreadId}", localUserId, threadId);
            return new ReplyCheckResult { ErrorMessage = "Failed to parse Gmail thread response." };
        }
    }

    private sealed record RefreshResult(bool Success, string? NewAccessToken, string? ErrorMessage);
    private sealed record ResolvedOAuthConfig(string ClientId, string ClientSecret);

    private async Task<RefreshResult> RefreshAccessTokenAsync(
        string refreshToken,
        UserEmailConnection connection,
        CancellationToken ct)
    {
        var oauthConfig = await ResolveOAuthConfigAsync(connection.UserId, ct);
        using var httpClient = _httpClientFactory.CreateClient();

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = oauthConfig.ClientId,
            ["client_secret"] = oauthConfig.ClientSecret,
            ["refresh_token"] = refreshToken,
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(TokenRefreshUrl, new FormUrlEncodedContent(formData), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailReplyMonitor - network error refreshing token for user {UserId}", connection.UserId);
            return new RefreshResult(false, null, $"Network error refreshing Gmail token: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GmailReplyMonitor - token refresh failed {Status} for user {UserId}: {Body}",
                response.StatusCode, connection.UserId, body);
            return new RefreshResult(false, null, "Failed to refresh Gmail token. Please reconnect your Gmail account.");
        }

        TokenRefreshResponse tokenResponse;
        try
        {
            tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(body)
                ?? throw new InvalidOperationException("Empty token refresh response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailReplyMonitor - could not parse token refresh response");
            return new RefreshResult(false, null, "Could not parse Gmail token refresh response.");
        }

        connection.AccessTokenEncrypted = _tokenProtector.Protect(tokenResponse.AccessToken);
        connection.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await _connectionRepo.UpsertAsync(connection, ct);

        return new RefreshResult(true, tokenResponse.AccessToken, null);
    }

    private async Task<ResolvedOAuthConfig> ResolveOAuthConfigAsync(Guid localUserId, CancellationToken ct)
    {
        var customCredential = await _oauthCredentialRepo.GetByUserAndProviderAsync(localUserId, "Gmail", ct);
        if (customCredential is not null)
        {
            try
            {
                var clientId = _oauthClientProtector.Unprotect(customCredential.ClientIdEncrypted);
                var clientSecret = _oauthClientProtector.Unprotect(customCredential.ClientSecretEncrypted);
                if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                {
                    return new ResolvedOAuthConfig(clientId, clientSecret);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "GmailReplyMonitor - failed to decrypt custom OAuth credentials for user {UserId}. Falling back to global config.",
                    localUserId);
            }
        }

        _oauthOptions.Validate();
        return new ResolvedOAuthConfig(_oauthOptions.ClientId, _oauthOptions.ClientSecret);
    }

    private sealed class TokenRefreshResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; } = 3600;
    }

    private static string BuildClassificationText(string? snippet, string? fullText)
    {
        var cleanedFullText = string.IsNullOrWhiteSpace(fullText) ? null : NormalizeWhitespace(fullText);
        if (!string.IsNullOrWhiteSpace(cleanedFullText))
        {
            return TruncateForClassification(cleanedFullText!, 4000);
        }

        var cleanedSnippet = string.IsNullOrWhiteSpace(snippet) ? string.Empty : NormalizeWhitespace(snippet);
        return TruncateForClassification(cleanedSnippet, 1000);
    }

    private static string TruncateForClassification(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxChars ? value : value[..maxChars];
    }

    private static string ExtractReadableMessageText(JsonElement payload)
    {
        var plainBuilder = new StringBuilder();
        var htmlBuilder = new StringBuilder();
        CollectMimeBody(payload, plainBuilder, htmlBuilder);

        var plainText = NormalizeWhitespace(plainBuilder.ToString());
        if (!string.IsNullOrWhiteSpace(plainText))
        {
            return plainText;
        }

        var htmlText = HtmlToPlainText(htmlBuilder.ToString());
        return NormalizeWhitespace(htmlText);
    }

    private static void CollectMimeBody(JsonElement part, StringBuilder plainBuilder, StringBuilder htmlBuilder)
    {
        var mimeType = part.TryGetProperty("mimeType", out var mimeEl)
            ? mimeEl.GetString()
            : null;

        if (part.TryGetProperty("body", out var bodyEl) &&
            bodyEl.TryGetProperty("data", out var dataEl))
        {
            var decoded = DecodeBase64Url(dataEl.GetString());
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                if (string.Equals(mimeType, "text/plain", StringComparison.OrdinalIgnoreCase))
                {
                    plainBuilder.AppendLine(decoded);
                }
                else if (string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase))
                {
                    htmlBuilder.AppendLine(decoded);
                }
            }
        }

        if (part.TryGetProperty("parts", out var partsEl) && partsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in partsEl.EnumerateArray())
            {
                CollectMimeBody(child, plainBuilder, htmlBuilder);
            }
        }
    }

    private static string DecodeBase64Url(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return string.Empty;
        }

        try
        {
            var normalized = encoded.Replace('-', '+').Replace('_', '/');
            var padLength = 4 - (normalized.Length % 4);
            if (padLength is > 0 and < 4)
            {
                normalized = normalized.PadRight(normalized.Length + padLength, '=');
            }

            var bytes = Convert.FromBase64String(normalized);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var noScripts = Regex.Replace(html, "<(script|style)[^>]*>.*?</\\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withBreaks = Regex.Replace(noScripts, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        var stripped = Regex.Replace(withBreaks, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(stripped);
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, "[\\t\\f\\v ]+", " ");
        normalized = Regex.Replace(normalized, "\\n{3,}", "\n\n");
        return normalized.Trim();
    }
}
