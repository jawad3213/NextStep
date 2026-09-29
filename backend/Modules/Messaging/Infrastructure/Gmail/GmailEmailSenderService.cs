using NextStep.Modules.Messaging.Application.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NextStep.Modules.Messaging.Infrastructure.Gmail;

/// <summary>
/// Sends emails through the Gmail API for a connected user account. Tokens are obtained from
/// <see cref="IGmailTokenProvider"/>; errors are returned as user-safe messages, never thrown.
/// </summary>
public class GmailEmailSenderService(
    IGmailTokenProvider tokens,
    IHttpClientFactory httpClientFactory,
    ILogger<GmailEmailSenderService> logger) : IEmailSenderService
{
    private const string GmailSendUrl = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";

    public async Task<SendEmailResult> SendAsync(
        Guid localUserId,
        string recipientEmail,
        string subject,
        string body,
        string? attachmentName = null,
        byte[]? attachmentBytes = null,
        string? attachmentContentType = null,
        CancellationToken cancellationToken = default)
    {
        // Addresses and file names go into raw MIME headers: a line break would inject headers.
        if (!IsValidAddress(recipientEmail))
            return Failed("The recipient email address is not valid.", needsReconnect: false);
        attachmentName = SanitizeFileName(attachmentName);

        var access = await tokens.GetAccessAsync(localUserId, forceRefresh: false, cancellationToken);
        if (!access.Success)
            return Failed(access.UserMessage, access.NeedsUserAction);

        var hasAttachment = attachmentBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentName);
        logger.LogInformation(
            "GmailSender - sending for user {UserId}. HasAttachment={HasAttachment}, AttachmentBytes={AttachmentSize}",
            localUserId, hasAttachment, attachmentBytes?.Length ?? 0);

        var mimeMessage = BuildMimeMessage(
            fromAddress: access.Connection!.EmailAddress,
            toAddress: recipientEmail,
            subject: subject,
            body: body,
            attachmentName: attachmentName,
            attachmentBytes: attachmentBytes,
            attachmentContentType: attachmentContentType);
        var payload = JsonSerializer.Serialize(new { raw = Base64UrlEncode(Encoding.ASCII.GetBytes(mimeMessage)) });

        var (status, responseBody, networkError) = await PostAsync(access.AccessToken!, payload, cancellationToken);

        // 401 with a token we believed valid: renew it once and retry before asking the user to reconnect.
        if (status == 401)
        {
            logger.LogInformation("GmailSender - 401 for user {UserId}; retrying with a refreshed token", localUserId);
            access = await tokens.GetAccessAsync(localUserId, forceRefresh: true, cancellationToken);
            if (!access.Success)
                return Failed(access.UserMessage, access.NeedsUserAction);
            (status, responseBody, networkError) = await PostAsync(access.AccessToken!, payload, cancellationToken);
        }

        if (networkError)
            return Failed(GmailMessages.Transient, needsReconnect: false);

        if (status is < 200 or >= 300)
        {
            var (failure, message) = GmailApiErrors.Describe((System.Net.HttpStatusCode)status, responseBody);
            logger.LogWarning("GmailSender - Gmail API returned {Status} for user {UserId} ({Failure})", status, localUserId, failure);
            if (failure is GmailFailure.Revoked or GmailFailure.MissingPermission)
                await tokens.MarkNeedsUserActionAsync(access.Connection!, failure, cancellationToken);
            return Failed(message, failure is GmailFailure.Revoked or GmailFailure.MissingPermission);
        }

        string? gmailMessageId = null, gmailThreadId = null;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("id", out var idProp)) gmailMessageId = idProp.GetString();
            if (doc.RootElement.TryGetProperty("threadId", out var threadProp)) gmailThreadId = threadProp.GetString();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "GmailSender - could not read Gmail message id/threadId");
        }

        logger.LogInformation("GmailSender - email sent for user {UserId}, message {MessageId}", localUserId, gmailMessageId);
        return new SendEmailResult { Success = true, ProviderMessageId = gmailMessageId, ProviderThreadId = gmailThreadId };
    }

    private async Task<(int Status, string Body, bool NetworkError)> PostAsync(string accessToken, string payload, CancellationToken ct)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, GmailSendUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await http.SendAsync(request, ct);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct), false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "GmailSender - Gmail API unreachable");
            return (0, string.Empty, true);
        }
    }

    internal static bool IsValidAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.IndexOfAny(['\r', '\n', '<', '>', ',', ';', '"']) >= 0)
            return false;
        return System.Net.Mail.MailAddress.TryCreate(address.Trim(), out var parsed) && parsed.Address == address.Trim();
    }

    internal static string? SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var clean = new string(name.Where(c => !char.IsControl(c) && c != '"' && c != (char)92).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "attachment" : clean;
    }

    private static SendEmailResult Failed(string? message, bool needsReconnect) => new()
    {
        Success = false,
        ErrorMessage = message ?? GmailMessages.Transient,
        NeedsReconnect = needsReconnect,
    };

    private static string BuildMimeMessage(
        string fromAddress,
        string toAddress,
        string subject,
        string body,
        string? attachmentName = null,
        byte[]? attachmentBytes = null,
        string? attachmentContentType = null)
    {
        const string crlf = "\r\n";
        var sb = new StringBuilder();
        sb.Append($"From: <{fromAddress}>{crlf}");
        sb.Append($"To: <{toAddress.Trim()}>{crlf}");
        sb.Append($"Subject: =?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?={crlf}");
        sb.Append($"MIME-Version: 1.0{crlf}");
        sb.Append($"Date: {DateTimeOffset.UtcNow:R}{crlf}");

        if (attachmentBytes != null && attachmentBytes.Length > 0 && !string.IsNullOrWhiteSpace(attachmentName))
        {
            var boundary = "----=_Part_" + Guid.NewGuid().ToString("N");
            sb.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"{crlf}{crlf}");
            sb.Append($"--{boundary}{crlf}");
            sb.Append($"Content-Type: text/plain; charset=UTF-8{crlf}");
            sb.Append($"Content-Transfer-Encoding: base64{crlf}{crlf}");
            sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(body)));
            sb.Append($"{crlf}{crlf}");
            sb.Append($"--{boundary}{crlf}");
            sb.Append($"Content-Type: {attachmentContentType ?? "application/octet-stream"}; name=\"{attachmentName}\"{crlf}");
            sb.Append($"Content-Disposition: attachment; filename=\"{attachmentName}\"{crlf}");
            sb.Append($"Content-Transfer-Encoding: base64{crlf}{crlf}");
            
            // Base64 encoding needs to be wrapped for large files (RFC 2045)
            var base64Attachment = Convert.ToBase64String(attachmentBytes);
            for (int i = 0; i < base64Attachment.Length; i += 76)
            {
                sb.Append(base64Attachment.Substring(i, Math.Min(76, base64Attachment.Length - i)));
                sb.Append(crlf);
            }
            sb.Append(crlf);
            sb.Append($"--{boundary}--{crlf}");
        }
        else
        {
            sb.Append($"Content-Type: text/plain; charset=UTF-8{crlf}");
            sb.Append($"Content-Transfer-Encoding: base64{crlf}{crlf}");
            sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(body)));
            sb.Append(crlf);
        }

        return sb.ToString();
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
