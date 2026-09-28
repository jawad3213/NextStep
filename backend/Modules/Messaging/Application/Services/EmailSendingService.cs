using NextStep.Modules.Messaging.Infrastructure.Gmail;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.CvDocuments.Contracts;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Modules.Messaging.Application.Dtos;
using NextStep.Modules.Messaging.Application.Mappings;
using NextStep.Modules.Messaging.Domain;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.Config;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Messaging.Application.Services;

public interface IEmailSendingService
{
    Task<SendEmailResultDto> SendDraftAsync(Guid draftId, Guid localUserId, CancellationToken cancellationToken = default);
    Task<EmailDraftDto> SendApplicationEmailAsync(Guid userId, SendApplicationEmailDto dto, CancellationToken cancellationToken = default);
}

/// <summary>Sends emails (Gmail API, SMTP fallback) with the final CV attached.</summary>
public class EmailSendingService : IEmailSendingService
{
    private readonly IEmailDraftRepository _emailDraftRepository;
    private readonly IApplicationsApi _applications;
    private readonly ICvDocumentsApi _cvDocuments;
    private readonly EmailDraftOwnership _ownership;
    private readonly IEmailSenderService _emailSenderService;
    private readonly SmtpEmailOptions _smtpOptions;
    private readonly ILogger<EmailSendingService> _logger;

    public EmailSendingService(
        IEmailDraftRepository emailDraftRepository,
        IApplicationsApi applications,
        ICvDocumentsApi cvDocuments,
        EmailDraftOwnership ownership,
        IEmailSenderService emailSenderService,
        IOptions<SmtpEmailOptions> smtpOptions,
        ILogger<EmailSendingService> logger)
    {
        _emailDraftRepository = emailDraftRepository;
        _applications = applications;
        _cvDocuments = cvDocuments;
        _ownership = ownership;
        _emailSenderService = emailSenderService;
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    public async Task<SendEmailResultDto> SendDraftAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        var draft = await _ownership.LoadOwnedAsync(draftId, localUserId, cancellationToken);

        if (draft.IsSent)
            throw new ConflictException("This draft has already been sent.");

        if (!draft.IsApproved)
            throw new ConflictException(
                "Draft must be approved before sending. Please approve the draft first.");

        if (string.IsNullOrWhiteSpace(draft.RecipientEmail))
            throw new ConflictException("RecipientEmail must be set before sending.");

        if (!IsValidEmail(draft.RecipientEmail))
            throw new ConflictException(
                $"RecipientEmail '{draft.RecipientEmail}' is not a valid email address.");

        if (string.IsNullOrWhiteSpace(draft.Subject))
            throw new ConflictException("Subject must not be empty before sending.");

        if (string.IsNullOrWhiteSpace(draft.Body))
            throw new ConflictException("Body must not be empty before sending.");

        draft.SendAttemptCount++;
        draft.UpdatedAtUtc = DateTime.UtcNow;

        string? attachmentName = null;
        byte[]? attachmentBytes = null;

        if (string.Equals(draft.EmailType, "application", StringComparison.OrdinalIgnoreCase))
        {
            var candidatureForAttachment = await _applications.GetApplicationAsync(draft.CandidatureId, cancellationToken);
            if (candidatureForAttachment?.UserId != localUserId)
                candidatureForAttachment = null;

            if (candidatureForAttachment is not null)
            {
                var cvHistoryId = candidatureForAttachment.OfferId.HasValue
                    ? await _cvDocuments.FindLatestFinalCvAsync(localUserId, candidatureForAttachment.OfferId.Value, cancellationToken)
                    : null;

                if (cvHistoryId.HasValue)
                {
                    var bytes = await _cvDocuments.GetCvPdfBytesAsync(localUserId, cvHistoryId.Value, cancellationToken);
                    if (bytes.Length > 0)
                    {
                        attachmentName = $"CV_{candidatureForAttachment.OfferId}.pdf";
                        attachmentBytes = bytes;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "EmailSendingService - draft {DraftId}: CV bytes are empty for history {CvHistoryId}; sending without attachment.",
                            draft.Id,
                            cvHistoryId.Value);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "EmailSendingService - draft {DraftId}: no final CV history found for offer {OfferId}; sending without attachment.",
                        draft.Id,
                        candidatureForAttachment.OfferId);
                }
            }
        }

        var result = await _emailSenderService.SendAsync(
            localUserId:       localUserId,
            recipientEmail:    draft.RecipientEmail,
            subject:           draft.Subject,
            body:              draft.Body,
            attachmentName:    attachmentName,
            attachmentBytes:   attachmentBytes,
            attachmentContentType: "application/pdf",
            cancellationToken: cancellationToken);

        if (result.Success)
        {
            draft.IsSent            = true;
            draft.SentAtUtc         = DateTime.UtcNow;
            draft.ProviderMessageId = result.ProviderMessageId;
            draft.ProviderThreadId  = result.ProviderThreadId;
            draft.ErrorMessage      = null;

            _logger.LogInformation(
                "EmailSendingService — draft {DraftId} sent for user {UserId}, Gmail message id: {GmailId}, thread id: {ThreadId}",
                draftId, localUserId, result.ProviderMessageId, result.ProviderThreadId);
        }
        else
        {
            draft.IsSent       = false;
            draft.ErrorMessage = result.ErrorMessage;

            _logger.LogWarning(
                "EmailSendingService — send failed for draft {DraftId}, user {UserId}: {Error}",
                draftId, localUserId, result.ErrorMessage);
        }

        await _emailDraftRepository.SaveChangesAsync(cancellationToken);

        // The application (owned by Applications) moves on once a follow-up was sent.
        if (result.Success && draft.EmailType == "relance")
        {
            await _applications.SetFollowUpStatusAsync(draft.CandidatureId, "RELANCE_ENVOYEE", cancellationToken);
            _logger.LogInformation(
                "EmailSendingService - candidature {CandidatureId} status updated to RELANCE_ENVOYEE",
                draft.CandidatureId);
        }

        return new SendEmailResultDto
        {
            Success           = result.Success,
            DraftId           = draft.Id,
            ProviderMessageId = result.ProviderMessageId,
            ProviderThreadId  = result.ProviderThreadId,
            ErrorMessage      = result.ErrorMessage,
            SentAtUtc         = draft.SentAtUtc
        };
    }

    public async Task<EmailDraftDto> SendApplicationEmailAsync(
        Guid userId,
        SendApplicationEmailDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RecipientEmail))
            throw new ConflictException("Recipient email is required.");
        if (string.IsNullOrWhiteSpace(dto.Subject))
            throw new ConflictException("Email subject is required.");
        if (string.IsNullOrWhiteSpace(dto.Body))
            throw new ConflictException("Email body is required.");

        var candidature = await _applications.FindApplicationForOfferAsync(userId, dto.OfferId, cancellationToken);

        if (candidature is null)
            throw new NotFoundException($"No candidature found for offer {dto.OfferId}.");

        var cvHistoryId = dto.CvHistoryId ?? await _cvDocuments.FindLatestFinalCvAsync(userId, dto.OfferId, cancellationToken);

        if (!cvHistoryId.HasValue)
            throw new ConflictException("No final CV was found to attach to the email.");

        var attachmentBytes = await _cvDocuments.GetCvPdfBytesAsync(userId, cvHistoryId.Value, cancellationToken);
        if (attachmentBytes is null || attachmentBytes.Length == 0)
            throw new ConflictException("Final CV attachment is empty. Please regenerate your CV and try again.");

        _logger.LogInformation(
            "EmailSendingService - sending application email with CV attachment for offer {OfferId}, user {UserId}, bytes={AttachmentBytes}",
            dto.OfferId,
            userId,
            attachmentBytes.Length);
        var draft = new EmailDraft
        {
            CandidatureId = candidature.CandidatureId,
            EmailType = string.IsNullOrWhiteSpace(dto.EmailType) ? "application" : dto.EmailType.Trim(),
            RecipientEmail = dto.RecipientEmail.Trim(),
            Subject = dto.Subject.Trim(),
            Body = dto.Body.Trim(),
            Language = string.IsNullOrWhiteSpace(dto.Language) ? "fr" : dto.Language.Trim(),
            IsApproved = true,
            IsSent = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        try
        {
            var gmailResult = await _emailSenderService.SendAsync(
                userId,
                draft.RecipientEmail!,
                draft.Subject,
                draft.Body,
                attachmentName: $"CV_{dto.OfferId}.pdf",
                attachmentBytes: attachmentBytes,
                attachmentContentType: "application/pdf",
                cancellationToken: cancellationToken);

            if (gmailResult.Success)
            {
                draft.IsSent = true;
                draft.ProviderMessageId = gmailResult.ProviderMessageId;
                draft.ProviderThreadId = gmailResult.ProviderThreadId;
                draft.SentAtUtc = DateTime.UtcNow;
                draft.UpdatedAtUtc = draft.SentAtUtc;
                draft.ErrorMessage = null;
            }
            else
            {
                var gmailError = string.IsNullOrWhiteSpace(gmailResult.ErrorMessage)
                    ? "Gmail sending failed."
                    : gmailResult.ErrorMessage;

                if (IsSmtpConfigured())
                {
                    _logger.LogWarning(
                        "EmailSendingService - Gmail sending failed for user {UserId}: {Error}. Falling back to SMTP...",
                        userId,
                        gmailError);

                    await SendEmailMessageAsync(draft, dto.OfferId, attachmentBytes, cancellationToken);
                    draft.IsSent = true;
                    draft.SentAtUtc = DateTime.UtcNow;
                    draft.UpdatedAtUtc = draft.SentAtUtc;
                    draft.ErrorMessage = null;
                }
                else
                {
                    _logger.LogWarning(
                        "EmailSendingService - Gmail sending failed for user {UserId}: {Error}. SMTP fallback disabled because SMTP is not configured.",
                        userId,
                        gmailError);

                    throw new ConflictException(gmailError);
                }
            }
        }
        catch (Exception ex)
        {
            draft.IsSent = false;
            draft.UpdatedAtUtc = DateTime.UtcNow;
            draft.ErrorMessage = ex.Message;
            _logger.LogError(ex, "EmailSendingService - failed to send application email for offer {OfferId}", dto.OfferId);
        }

        await _emailDraftRepository.AddAsync(draft, cancellationToken);

        if (!draft.IsSent)
            throw new ConflictException(draft.ErrorMessage ?? "Email sending failed.");

        return draft.ToDto();
    }

    private async Task SendEmailMessageAsync(EmailDraft draft, Guid offerId, byte[] attachmentBytes, CancellationToken cancellationToken)
    {
        ValidateSmtpConfiguration();

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = draft.Subject,
            Body = draft.Body,
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8,
        };

        message.To.Add(draft.RecipientEmail!);
        var attachmentStream = new MemoryStream(attachmentBytes, writable: false);
        var attachment = new Attachment(attachmentStream, $"CV_{offerId}.pdf", "application/pdf");
        message.Attachments.Add(attachment);

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(_smtpOptions.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password)
        };

        using var ctr = cancellationToken.Register(() => client.SendAsyncCancel());
        await client.SendMailAsync(message, cancellationToken);
    }

    private void ValidateSmtpConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host)
            || string.IsNullOrWhiteSpace(_smtpOptions.FromEmail))
        {
            throw new ConflictException("SMTP is not configured. Please set Email:Smtp:Host and Email:Smtp:FromEmail.");
        }
    }

    private bool IsSmtpConfigured()
    {
        return !string.IsNullOrWhiteSpace(_smtpOptions.Host)
            && !string.IsNullOrWhiteSpace(_smtpOptions.FromEmail);
    }

    private static bool IsValidEmail(string email)
    {
        try   { var _ = new MailAddress(email); return true; }
        catch { return false; }
    }
}
