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
/// Sends emails through Gmail API for a connected user account.
/// Tokens and optional BYO OAuth credentials are decrypted only on backend.
/// </summary>
public class GmailEmailSenderService : IEmailSenderService
{
    private const string TokenProtectionPurpose = "GmailOAuthTokens";
    private const string OAuthClientProtectionPurpose = "GmailOAuthClientCredentials";
    private const string GmailSendUrl = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";
    private const string TokenRefreshUrl = "https://oauth2.googleapis.com/token";

    private readonly IUserEmailConnectionRepository _connectionRepo;
    private readonly IUserOAuthCredentialRepository _oauthCredentialRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _tokenProtector;
    private readonly IDataProtector _oauthClientProtector;
    private readonly GoogleOAuthOptions _oauthOptions;
    private readonly ILogger<GmailEmailSenderService> _logger;

    public GmailEmailSenderService(
        IUserEmailConnectionRepository connectionRepo,
        IUserOAuthCredentialRepository oauthCredentialRepo,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<GoogleOAuthOptions> oauthOptions,
        ILogger<GmailEmailSenderService> logger)
    {
        _connectionRepo = connectionRepo;
        _oauthCredentialRepo = oauthCredentialRepo;
        _httpClientFactory = httpClientFactory;
        _tokenProtector = dataProtectionProvider.CreateProtector(TokenProtectionPurpose);
        _oauthClientProtector = dataProtectionProvider.CreateProtector(OAuthClientProtectionPurpose);
        _oauthOptions = oauthOptions.Value;
        _logger = logger;
    }

    public async Task<SendEmailResult> SendAsync(
        Guid localUserId,
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionRepo.GetByUserAndProviderAsync(localUserId, "Gmail", cancellationToken);
        if (connection is null)
        {
            return new SendEmailResult
            {
                Success = false,
                ErrorMessage = "Gmail account is not connected. Please connect your Gmail account before sending."
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
            _logger.LogError(ex, "GmailSender - failed to decrypt tokens for user {UserId}", localUserId);
            return new SendEmailResult
            {
                Success = false,
                ErrorMessage = "Failed to decrypt Gmail tokens. Please reconnect your Gmail account."
            };
        }

        if (DateTime.UtcNow >= connection.AccessTokenExpiresAtUtc.AddSeconds(-30))
        {
            var refreshResult = await RefreshAccessTokenAsync(refreshToken, connection, cancellationToken);
            if (!refreshResult.Success)
            {
                return new SendEmailResult
                {
                    Success = false,
                    ErrorMessage = refreshResult.ErrorMessage
                };
            }

            accessToken = refreshResult.NewAccessToken!;
        }

        var mimeMessage = BuildMimeMessage(
            fromAddress: connection.EmailAddress,
            toAddress: recipientEmail,
            subject: subject,
            body: body);

        var rawBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(mimeMessage));

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var payload = JsonSerializer.Serialize(new { raw = rawBase64 });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(GmailSendUrl, content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailSender - HTTP error calling Gmail API for user {UserId}", localUserId);
            return new SendEmailResult
            {
                Success = false,
                ErrorMessage = $"Network error contacting Gmail API: {ex.Message}"
            };
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GmailSender - Gmail API returned {Status} for user {UserId}: {Body}",
                response.StatusCode, localUserId, responseBody);

            return new SendEmailResult
            {
                Success = false,
                ErrorMessage = $"Gmail API error {(int)response.StatusCode}: {responseBody}"
            };
        }

        string? gmailMessageId = null;
        string? gmailThreadId = null;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
                gmailMessageId = idProp.GetString();
            if (doc.RootElement.TryGetProperty("threadId", out var threadProp))
                gmailThreadId = threadProp.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GmailSender - could not parse Gmail response id/threadId");
        }

        _logger.LogInformation(
            "GmailSender - email sent for user {UserId}, Gmail message id: {MessageId}, thread id: {ThreadId}",
            localUserId, gmailMessageId, gmailThreadId);

        return new SendEmailResult
        {
            Success = true,
            ProviderMessageId = gmailMessageId,
            ProviderThreadId = gmailThreadId
        };
    }

    private sealed record RefreshResult(bool Success, string? NewAccessToken, string? ErrorMessage);
    private sealed record ResolvedOAuthConfig(string ClientId, string ClientSecret);

    private async Task<RefreshResult> RefreshAccessTokenAsync(
        string refreshToken,
        UserEmailConnection connection,
        CancellationToken cancellationToken)
    {
        var oauthConfig = await ResolveOAuthConfigAsync(connection.UserId, cancellationToken);
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
            response = await httpClient.PostAsync(
                TokenRefreshUrl,
                new FormUrlEncodedContent(formData),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailSender - network error refreshing token for user {UserId}", connection.UserId);
            return new RefreshResult(false, null, $"Network error refreshing Gmail token: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GmailSender - token refresh failed {Status} for user {UserId}: {Body}",
                response.StatusCode, connection.UserId, body);
            return new RefreshResult(false, null, "Failed to refresh Gmail access token. Please reconnect your Gmail account.");
        }

        TokenResponse tokenResponse;
        try
        {
            tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body)
                ?? throw new InvalidOperationException("Empty token response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GmailSender - could not parse refresh token response");
            return new RefreshResult(false, null, "Could not parse Gmail token refresh response.");
        }

        connection.AccessTokenEncrypted = _tokenProtector.Protect(tokenResponse.AccessToken);
        connection.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await _connectionRepo.UpsertAsync(connection, cancellationToken);

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
                    "GmailSender - failed to decrypt custom OAuth credentials for user {UserId}. Falling back to global config.",
                    localUserId);
            }
        }

        _oauthOptions.Validate();
        return new ResolvedOAuthConfig(_oauthOptions.ClientId, _oauthOptions.ClientSecret);
    }

    private static string BuildMimeMessage(
        string fromAddress,
        string toAddress,
        string subject,
        string body)
    {
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
