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

public interface IReplyEmailService
{
    Task<EmailDraftDto> GenerateReplyDraftAsync(GenerateReplyDraftDto dto, Guid localUserId, CancellationToken cancellationToken = default);
}

/// <summary>Reply drafts to a detected recruiter response.</summary>
public class ReplyEmailService : IReplyEmailService
{
    private readonly IEmailDraftRepository _emailDraftRepository;
    private readonly IApplicationsApi _applications;
    private readonly EmailContextBuilder _contextBuilder;
    private readonly IAgentHttpClient _agentHttpClient;
    private readonly MessagingDbContext _db;
    private readonly ILogger<ReplyEmailService> _logger;

    public ReplyEmailService(
        IEmailDraftRepository emailDraftRepository,
        IApplicationsApi applications,
        EmailContextBuilder contextBuilder,
        IAgentHttpClient agentHttpClient,
        MessagingDbContext db,
        ILogger<ReplyEmailService> logger)
    {
        _emailDraftRepository = emailDraftRepository;
        _applications = applications;
        _contextBuilder = contextBuilder;
        _agentHttpClient = agentHttpClient;
        _db = db;
        _logger = logger;
    }

    public async Task<EmailDraftDto> GenerateReplyDraftAsync(
        GenerateReplyDraftDto dto,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Load and verify candidature ───────────────────────────────────
        var candidature = await _applications.GetApplicationAsync(dto.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new NotFoundException($"Candidature {dto.CandidatureId} not found.");

        if (candidature.UserId != localUserId)
            throw new ForbiddenException(
                $"User {localUserId} does not own candidature {dto.CandidatureId}.");

        // ── 2. Guard: reply must have been detected ───────────────────────────
        if (!candidature.HasResponse)
            throw new ConflictException(
                "Cannot generate reply draft because no recruiter response has been detected for this candidature.");

        // ── 3. Guard: need some reply context ────────────────────────────────
        if (string.IsNullOrWhiteSpace(candidature.LastResponseSnippet) &&
            string.IsNullOrWhiteSpace(candidature.ResponseSummary))
            throw new ConflictException(
                "Cannot generate reply draft because the recruiter response context (snippet/summary) is missing.");

        // ── 4. Duplicate prevention: return existing unsent reply draft ──────
        var existingUnsentReply = await _db.EmailDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.CandidatureId == candidature.CandidatureId &&
                d.EmailType     == "reply" &&
                !d.IsSent,
                cancellationToken);

        if (existingUnsentReply is not null)
        {
            _logger.LogWarning(
                "ReplyEmailService — unsent reply draft {DraftId} already exists for candidature {CandidatureId}. Returning existing draft.",
                existingUnsentReply.Id, dto.CandidatureId);
            return existingUnsentReply.ToDto();
        }

        // ── 5. Find previous sent email for context ───────────────────────────
        var previousDraft = await _db.EmailDrafts
            .AsNoTracking()
            .Where(d =>
                d.CandidatureId == candidature.CandidatureId &&
                (d.EmailType == "application" || d.EmailType == "relance") &&
                d.IsSent &&
                d.SentAtUtc != null)
            .OrderByDescending(d => d.SentAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // ── 6. Load candidate + offer context ────────────────────────────────
        var ctx = await _contextBuilder.BuildAsync(
            candidature.UserId, candidature.OfferId!.Value, cancellationToken);

        // ── 7. Resolve recipient email ────────────────────────────────────────
        // Prefer LastResponseFrom (parse "Name <email>" format safely).
        // Fallback to previousDraft.RecipientEmail. Null if neither is valid.
        string? recipientEmail = TryParseEmailAddress(candidature.LastResponseFrom)
                                 ?? previousDraft?.RecipientEmail;

        // ── 8. Build Python payload ──────────────────────────────────────────
        var pythonRequest = new
        {
            candidature_id = candidature.CandidatureId.ToString(),
            candidate      = EmailContextBuilder.CandidatePayload(ctx),
            job_offer      = EmailContextBuilder.JobOfferPayload(ctx),
            previous_email = previousDraft is null ? null : new
            {
                subject     = previousDraft.Subject,
                body        = previousDraft.Body is { Length: > 0 } b
                                  ? b[..Math.Min(1500, b.Length)]
                                  : previousDraft.Body,
                sent_at_utc = previousDraft.SentAtUtc?.ToString("o"),
            },
            recruiter_reply = new
            {
                from_email       = candidature.LastResponseFrom,
                snippet          = candidature.LastResponseSnippet ?? candidature.ResponseSummary ?? string.Empty,
                received_at_utc  = candidature.LastResponseAtUtc?.ToString("o"),
            },
            response_type      = candidature.ResponseStatus,
            response_summary   = candidature.ResponseSummary,
            recommended_action = candidature.RecommendedAction,
            language           = dto.Language,
            tone               = dto.Tone,
            user_instructions  = dto.UserInstructions,
        };

        _logger.LogInformation(
            "ReplyEmailService — calling Python /email/generate-reply for candidature {CandidatureId} | responseType={ResponseType}",
            dto.CandidatureId, candidature.ResponseStatus);

        // ── 9. Call Python agent ──────────────────────────────────────────────
        AgentEmailResponse pythonResponse;
        try
        {
            pythonResponse = await _agentHttpClient
                .PostAsync<object, AgentEmailResponse>(
                    "/email/generate-reply",
                    pythonRequest,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ReplyEmailService — Python reply agent failed for candidature {CandidatureId}",
                dto.CandidatureId);
            throw new ConflictException(
                $"Reply email generation failed: {ex.Message}", ex);
        }

        // ── 10. Save reply draft ─────────────────────────────────────────────
        // IMPORTANT: Do NOT modify candidature.ResponseStatus or candidature.Statut here.
        // Classification information must remain visible to the user.
        var draft = new EmailDraft
        {
            CandidatureId  = candidature.CandidatureId,
            EmailType      = "reply",
            RecipientEmail = recipientEmail,
            Subject        = pythonResponse.Subject,
            Body           = pythonResponse.Body,
            Language       = string.IsNullOrWhiteSpace(pythonResponse.Language)
                                 ? dto.Language
                                 : pythonResponse.Language,
            IsApproved     = false,
            IsSent         = false,
            CreatedAtUtc   = DateTime.UtcNow,
        };

        await _emailDraftRepository.AddAsync(draft, cancellationToken);
        await _emailDraftRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ReplyEmailService — reply draft {DraftId} saved for candidature {CandidatureId} | subject: {Subject} | recipient: {Recipient}",
            draft.Id, dto.CandidatureId, draft.Subject, recipientEmail ?? "(none — user must set)");

        return draft.ToDto();
    }

    /// <summary>
    /// Parses a raw email header value such as "Name &lt;email@domain.com&gt;" or
    /// "email@domain.com" and returns the address part if valid; otherwise null.
    /// </summary>
    private static string? TryParseEmailAddress(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            var addr = new MailAddress(raw.Trim());
            return addr.Address;
        }
        catch { return null; }
    }
}
