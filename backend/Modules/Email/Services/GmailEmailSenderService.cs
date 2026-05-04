using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NextStep.Modules.Email.Models;
using NextStep.Modules.Email.Repositories;
using NextStep.Shared.Config;

namespace NextStep.Modules.Email.Services;

/// <summary>
/// Sends emails through the Gmail API using the user's connected OAuth account.
/// Handles token decryption, automatic token refresh, MIME building, and Gmail API calling.
/// </summary>
public class GmailEmailSenderService : IEmailSenderService
{
    private const string DataProtectionPurpose = "GmailOAuthTokens";
    private const string GmailSendUrl          = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";
    private const string TokenRefreshUrl       = "https://oauth2.googleapis.com/token";

    private readonly IUserEmailConnectionRepository _connectionRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly GoogleOAuthOptions _oauthOptions;
    private readonly ILogger<GmailEmailSenderService> _logger;

    public GmailEmailSenderService(
        IUserEmailConnectionRepository connectionRepo,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<GoogleOAuthOptions> oauthOptions,
        ILogger<GmailEmailSenderService> logger)
    {
        _connectionRepo    = connectionRepo;
        _httpClientFactory = httpClientFactory;
        _protector         = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _oauthOptions      = oauthOptions.Value;
        _logger            = logger;
    }

    public async Task<SendEmailResult> SendAsync(
        Guid localUserId,
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Load Gmail connection ────────────────────────────────────────────
        var connection = await _connectionRepo.GetByUserAndProviderAsync(
            localUserId, "Gmail", cancellationToken);

        if (connection is null)
        {
            return new SendEmailResult
            {
                Success      = false,
                ErrorMessage = "Gmail account is not connected. Please connect your Gmail account before sending."
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
            _logger.LogError(ex, "GmailSender — failed to decrypt tokens for user {UserId}", localUserId);
            return new SendEmailResult
            {
                Success      = false,
                ErrorMessage = "Failed to decrypt Gmail tokens. Please reconnect your Gmail account."
            };
        }

        // ── 3. Refresh access token if expired ─────────────────────────────────
        if (DateTime.UtcNow >= connection.AccessTokenExpiresAtUtc.AddSeconds(-30))
        {
            var refreshResult = await RefreshAccessTokenAsync(
                refreshToken, connection, cancellationToken);

            if (!refreshResult.Success)
                return new SendEmailResult
                {
                    Success      = false,
                    ErrorMessage = refreshResult.ErrorMessage
                };

            accessToken = refreshResult.NewAccessToken!;
        }

        // ── 4. Build MIME email ─────────────────────────────────────────────────
        var mimeMessage = BuildMimeMessage(
            fromAddress: connection.EmailAddress,
            toAddress:   recipientEmail,
            subject:     subject,
            body:        body);

        var rawBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(mimeMessage));

        // ── 5. Call Gmail API ────────────────────────────────────────────────────
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var payload = JsonSerializer.Serialize(new { raw = rawBase64 });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(GmailSendUrl, content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailSender — HTTP error calling Gmail API for user {UserId}", localUserId);
            return new SendEmailResult
            {
                Success      = false,
                ErrorMessage = $"Network error contacting Gmail API: {ex.Message}"
            };
        }

        // ── 6. Parse response ───────────────────────────────────────────────────
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GmailSender — Gmail API returned {Status} for user {UserId}: {Body}",
                response.StatusCode, localUserId, responseBody);

            return new SendEmailResult
            {
                Success      = false,
                ErrorMessage = $"Gmail API error {(int)response.StatusCode}: {responseBody}"
            };
        }

        string? gmailMessageId = null;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
                gmailMessageId = idProp.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GmailSender — could not parse Gmail response id");
        }

        _logger.LogInformation(
            "GmailSender — email sent for user {UserId}, Gmail message id: {MessageId}",
            localUserId, gmailMessageId);

        return new SendEmailResult
        {
            Success           = true,
            ProviderMessageId = gmailMessageId
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private sealed record RefreshResult(bool Success, string? NewAccessToken, string? ErrorMessage);

    /// <summary>
    /// Refreshes an expired access token and persists the updated encrypted token.
    /// </summary>
    private async Task<RefreshResult> RefreshAccessTokenAsync(
        string refreshToken,
        UserEmailConnection connection,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "GmailSender — refreshing access token for user {UserId}", connection.UserId);

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
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailSender — network error refreshing token for user {UserId}", connection.UserId);
            return new RefreshResult(false, null, $"Network error refreshing Gmail token: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GmailSender — token refresh failed {Status} for user {UserId}: {Body}",
                response.StatusCode, connection.UserId, body);
            return new RefreshResult(false, null,
                "Failed to refresh Gmail access token. Please reconnect your Gmail account.");
        }

        TokenResponse tokenResponse;
        try
        {
            tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body)
                ?? throw new InvalidOperationException("Empty token response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailSender — could not parse refresh token response");
            return new RefreshResult(false, null, "Could not parse Gmail token refresh response.");
        }

        // Persist the refreshed access token (encrypted)
        connection.AccessTokenEncrypted    = _protector.Protect(tokenResponse.AccessToken);
        connection.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);
        connection.UpdatedAtUtc            = DateTime.UtcNow;
        await _connectionRepo.UpsertAsync(connection, cancellationToken);

        return new RefreshResult(true, tokenResponse.AccessToken, null);
    }

    private static string BuildMimeMessage(
        string fromAddress,
        string toAddress,
        string subject,
        string body)
    {
        // RFC 2822 MIME format required by Gmail API
        var sb = new StringBuilder();
        sb.AppendLine($"From: {fromAddress}");
        sb.AppendLine($"To: {toAddress}");
        sb.AppendLine($"Subject: =?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine("Content-Type: text/plain; charset=utf-8");
        sb.AppendLine("Content-Transfer-Encoding: base64");
        sb.AppendLine();
        sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(body)));
        return sb.ToString();
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; } = 3600;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";
    }
}
