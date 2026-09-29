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

public interface IFollowUpEmailService
{
    Task<EmailDraftDto> GenerateFollowUpDraftAsync(GenerateFollowUpDraftDto dto, Guid localUserId, CancellationToken cancellationToken = default);
}

/// <summary>Follow-up (relance) drafts when the recruiter has not answered.</summary>
public class FollowUpEmailService : IFollowUpEmailService
{
    private readonly IEmailDraftRepository _emailDraftRepository;
    private readonly IApplicationsApi _applications;
    private readonly EmailContextBuilder _contextBuilder;
    private readonly IAgentHttpClient _agentHttpClient;
    private readonly MessagingDbContext _db;
    private readonly EmailFollowUpOptions _followUpOptions;
    private readonly ILogger<FollowUpEmailService> _logger;

    public FollowUpEmailService(
        IEmailDraftRepository emailDraftRepository,
        IApplicationsApi applications,
        EmailContextBuilder contextBuilder,
        IAgentHttpClient agentHttpClient,
        MessagingDbContext db,
        IOptions<EmailFollowUpOptions> followUpOptions,
        ILogger<FollowUpEmailService> logger)
    {
        _emailDraftRepository = emailDraftRepository;
        _applications = applications;
        _contextBuilder = contextBuilder;
        _agentHttpClient = agentHttpClient;
        _db = db;
        _followUpOptions = followUpOptions.Value;
        _logger = logger;
    }

    public async Task<EmailDraftDto> GenerateFollowUpDraftAsync(
        GenerateFollowUpDraftDto dto,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Load and verify candidature (with tracking) ───────────────────
        var candidature = await _applications.GetApplicationAsync(dto.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new NotFoundException($"Candidature {dto.CandidatureId} not found.");

        if (candidature.UserId != localUserId)
            throw new ForbiddenException(
                $"User {localUserId} does not own candidature {dto.CandidatureId}.");

        // ── 2. Block if a reply was already received ─────────────────────────
        if (candidature.HasResponse)
            throw new ConflictException(
                "A response has already been received for this candidature. Follow-up is not needed.");

        // ── 3. Check MaxFollowUps limit ──────────────────────────────────────
        int maxFollowUps = _followUpOptions.MaxFollowUps;

        int sentRelanceCount = await _db.EmailDrafts
            .CountAsync(d =>
                d.CandidatureId == candidature.CandidatureId &&
                d.EmailType == "relance" &&
                d.IsSent,
                cancellationToken);

        if (sentRelanceCount >= maxFollowUps)
            throw new ConflictException(
                $"Maximum number of follow-ups ({maxFollowUps}) reached for this candidature.");

        // ── 4. Return existing unsent relance draft if one already exists ────
        var existingUnsentRelance = await _db.EmailDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.CandidatureId == candidature.CandidatureId &&
                d.EmailType == "relance" &&
                !d.IsSent,
                cancellationToken);

        if (existingUnsentRelance is not null)
        {
            _logger.LogWarning(
                "FollowUpEmailService — unsent relance draft {DraftId} already exists for candidature {CandidatureId}. Returning existing draft.",
                existingUnsentRelance.Id, dto.CandidatureId);
            return existingUnsentRelance.ToDto();
        }

        // ── 5. Find previous email context ───────────────────────────────────
        // If relances have been sent, follow up on the latest relance.
        // Otherwise, follow up on the latest sent application email.
        EmailDraft? previousDraft;

        if (sentRelanceCount > 0)
        {
            previousDraft = await _db.EmailDrafts
                .AsNoTracking()
                .Where(d =>
                    d.CandidatureId == candidature.CandidatureId &&
                    d.EmailType == "relance" &&
                    d.IsSent &&
                    d.SentAtUtc != null)
                .OrderByDescending(d => d.SentAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            previousDraft = await _db.EmailDrafts
                .AsNoTracking()
                .Where(d =>
                    d.CandidatureId == candidature.CandidatureId &&
                    d.IsSent &&
                    d.SentAtUtc != null)
                .OrderByDescending(d => d.SentAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (previousDraft is null)
            throw new ConflictException(
                "Cannot generate a follow-up because no sent email exists for this candidature.");

        int daysSinceSent = (int)(DateTime.UtcNow - previousDraft.SentAtUtc!.Value).TotalDays;

        // ── 6. Load profile and offer context ────────────────────────────────
        var ctx = await _contextBuilder.BuildAsync(
            candidature.UserId, candidature.OfferId!.Value, cancellationToken);

        // ── 7. Build Python payload ──────────────────────────────────────────
        var pythonRequest = new
        {
            candidature_id = candidature.CandidatureId.ToString(),
            candidate      = EmailContextBuilder.CandidatePayload(ctx),
            job_offer      = EmailContextBuilder.JobOfferPayload(ctx),
            previous_email = new
            {
                subject     = previousDraft.Subject,
                body        = previousDraft.Body,
                sent_at_utc = previousDraft.SentAtUtc?.ToString("o"),
            },
            options = new
            {
                language        = dto.Language,
                tone            = dto.Tone,
                days_since_sent = daysSinceSent,
            },
        };

        _logger.LogInformation(
            "FollowUpEmailService — calling Python /email/generate-follow-up for candidature {CandidatureId} | sentRelances={Count} | daysSinceSent={Days}",
            dto.CandidatureId, sentRelanceCount, daysSinceSent);

        AgentEmailResponse pythonResponse;
        try
        {
            pythonResponse = await _agentHttpClient
                .PostAsync<object, AgentEmailResponse>(
                    "/email/generate-follow-up",
                    pythonRequest,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FollowUpEmailService — Python follow-up agent failed for candidature {CandidatureId}",
                dto.CandidatureId);
            throw new ConflictException(
                $"Follow-up email generation failed: {ex.Message}", ex);
        }

        // ── 8. Save relance draft ────────────────────────────────────────────
        var draft = new EmailDraft
        {
            CandidatureId  = candidature.CandidatureId,
            EmailType      = "relance",
            RecipientEmail = previousDraft.RecipientEmail,  // carry over from previous email
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

        // 9. Update the application status (owned by Applications)
        await _applications.SetFollowUpStatusAsync(candidature.CandidatureId, "RELANCE_GENEREE", cancellationToken);

        _logger.LogInformation(
            "FollowUpEmailService — relance draft {DraftId} saved for candidature {CandidatureId} | subject: {Subject}",
            draft.Id, dto.CandidatureId, draft.Subject);

        return draft.ToDto();
    }
}
