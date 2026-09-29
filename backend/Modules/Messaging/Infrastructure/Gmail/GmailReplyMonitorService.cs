using NextStep.Modules.Messaging.Application.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NextStep.Modules.Messaging.Domain;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Shared.Config;

namespace NextStep.Modules.Messaging.Infrastructure.Gmail;

/// <summary>
/// Checks Gmail threads for recruiter replies.
/// Uses encrypted OAuth tokens and supports per-user BYO OAuth credentials.
/// </summary>
public class GmailReplyMonitorService(
    IGmailTokenProvider tokens,
    IHttpClientFactory httpClientFactory,
    ILogger<GmailReplyMonitorService> logger) : IGmailReplyMonitorService
{
    private readonly ILogger<GmailReplyMonitorService> _logger = logger;

    public async Task<ReplyCheckResult> CheckThreadForReplyAsync(
        Guid localUserId,
        string threadId,
        DateTime sentAtUtc,
        CancellationToken ct = default)
    {
        var access = await tokens.GetAccessAsync(localUserId, forceRefresh: false, ct, skipIfReconnectRequired: true);
        if (!access.Success)
            return new ReplyCheckResult { ErrorMessage = access.UserMessage };
        var connection = access.Connection!;

        var threadUrl = $"https://gmail.googleapis.com/gmail/v1/users/me/threads/{Uri.EscapeDataString(threadId)}?format=full";
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, threadUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
            response = await httpClientFactory.CreateClient().SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "GmailReplyMonitor - Gmail unreachable for thread {ThreadId}, user {UserId}", threadId, localUserId);
            return new ReplyCheckResult { ErrorMessage = GmailMessages.Transient };
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var (failure, message) = GmailApiErrors.Describe(response.StatusCode, errorBody);
            _logger.LogWarning("GmailReplyMonitor - Gmail API {Status} for user {UserId}, thread {ThreadId} ({Failure})",
                (int)response.StatusCode, localUserId, threadId, failure);
            if (failure is GmailFailure.Revoked or GmailFailure.MissingPermission)
                await tokens.MarkNeedsUserActionAsync(connection, failure, ct);
            return new ReplyCheckResult { ErrorMessage = message };
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
