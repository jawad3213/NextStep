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

public interface IEmailDraftService
{
    Task<EmailDraftDto> GenerateDraftAsync(Guid localUserId, GenerateEmailDraftDto dto, CancellationToken cancellationToken = default);
    Task<EmailDraftDto> GetDraftByIdAsync(Guid draftId, Guid localUserId, CancellationToken cancellationToken = default);
    Task<List<EmailDraftDto>> GetDraftsByCandidatureAsync(Guid candidatureId, Guid localUserId, CancellationToken cancellationToken = default);
    Task<EmailDraftDto> UpdateDraftAsync(Guid draftId, Guid localUserId, UpdateEmailDraftDto dto, CancellationToken cancellationToken = default);
    Task<EmailDraftDto> ApproveDraftAsync(Guid draftId, Guid localUserId, CancellationToken cancellationToken = default);
}

/// <summary>Application email drafts: AI generation, reading, editing and approval.</summary>
public class EmailDraftService : IEmailDraftService
{
    private readonly IEmailDraftRepository _emailDraftRepository;
    private readonly IApplicationsApi _applications;
    private readonly EmailContextBuilder _contextBuilder;
    private readonly EmailDraftOwnership _ownership;
    private readonly IAgentHttpClient _agentHttpClient;
    private readonly ILogger<EmailDraftService> _logger;

    public EmailDraftService(
        IEmailDraftRepository emailDraftRepository,
        IApplicationsApi applications,
        EmailContextBuilder contextBuilder,
        EmailDraftOwnership ownership,
        IAgentHttpClient agentHttpClient,
        ILogger<EmailDraftService> logger)
    {
        _emailDraftRepository = emailDraftRepository;
        _applications = applications;
        _contextBuilder = contextBuilder;
        _ownership = ownership;
        _agentHttpClient = agentHttpClient;
        _logger = logger;
    }

    public async Task<EmailDraftDto> GenerateDraftAsync(
        Guid localUserId,
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default)
    {
        var candidature = await _applications.GetApplicationAsync(
            dto.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new NotFoundException($"Candidature {dto.CandidatureId} not found.");

        if (candidature.UserId != localUserId)
            throw new ForbiddenException($"User {localUserId} does not own candidature {dto.CandidatureId}.");

        var ctx = await _contextBuilder.BuildAsync(candidature.UserId, candidature.OfferId!.Value, cancellationToken);

        var pythonRequest = new
        {
            candidature_id = candidature.CandidatureId.ToString(),
            candidate      = EmailContextBuilder.CandidatePayload(ctx),
            job_offer      = EmailContextBuilder.JobOfferPayload(ctx),
            options = new
            {
                language                  = dto.Language,
                tone                      = dto.Tone,
                include_motivation_letter = dto.IncludeMotivationLetter,
            },
            skill_gap            = EmailContextBuilder.DeserializeJsonObjectOrNull(ctx.SkillGapJson),
            company_intelligence = EmailContextBuilder.DeserializeJsonObjectOrNull(ctx.CompanyIntelligenceJson),
            cv_history_id        = dto.CvHistoryId.HasValue ? dto.CvHistoryId.Value.ToString() : (string?)null
        };

        _logger.LogInformation(
            "EmailDraftService — calling Python /email/generate for candidature {CandidatureId}",
            dto.CandidatureId);

        AgentEmailResponse pythonResponse;
        var usedFallbackDraft = false;
        try
        {
            pythonResponse = await _agentHttpClient
                .PostAsync<object, AgentEmailResponse>(
                    "/email/generate",
                    pythonRequest,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "EmailDraftService — Python agent failed for candidature {CandidatureId}. Falling back to deterministic draft.",
                dto.CandidatureId);
            pythonResponse = BuildFallbackApplicationDraft(ctx, dto.Language);
            usedFallbackDraft = true;
        }
        var draft = new EmailDraft
        {
            CandidatureId  = candidature.CandidatureId,
            EmailType      = dto.EmailType,
            RecipientEmail = null,
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

        if (usedFallbackDraft)
        {
            _logger.LogInformation(
                "EmailDraftService — fallback draft saved for candidature {CandidatureId} | subject: {Subject}",
                dto.CandidatureId, draft.Subject);
        }
        else
        {
            _logger.LogInformation(
                "EmailDraftService — draft saved for candidature {CandidatureId} | subject: {Subject}",
                dto.CandidatureId, draft.Subject);
        }

        return draft.ToDto();
    }

    public async Task<EmailDraftDto> GetDraftByIdAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        // LoadAndVerifyOwnershipAsync throws KeyNotFoundException / UnauthorizedAccessException
        var draft = await _ownership.LoadOwnedAsync(draftId, localUserId, cancellationToken);
        return draft.ToDto();
    }

    public async Task<List<EmailDraftDto>> GetDraftsByCandidatureAsync(
        Guid candidatureId,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        await _ownership.EnsureApplicationOwnedAsync(candidatureId, localUserId, cancellationToken);

        var drafts = await _emailDraftRepository.GetByCandidatureIdAsync(
            candidatureId, cancellationToken);

        return drafts.Select(d => d.ToDto()).ToList();
    }

    public async Task<EmailDraftDto> UpdateDraftAsync(
        Guid draftId,
        Guid localUserId,
        UpdateEmailDraftDto dto,
        CancellationToken cancellationToken = default)
    {
        var draft = await _ownership.LoadOwnedAsync(draftId, localUserId, cancellationToken);

        if (draft.IsSent)
            throw new ConflictException("Cannot update a draft that has already been sent.");

        if (dto.RecipientEmail is not null)
            draft.RecipientEmail = dto.RecipientEmail.Trim();

        if (dto.Subject is not null)
            draft.Subject = dto.Subject.Trim();

        if (dto.Body is not null)
            draft.Body = dto.Body;

        draft.UpdatedAtUtc = DateTime.UtcNow;

        await _emailDraftRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "EmailDraftService — draft {DraftId} updated by user {UserId}", draftId, localUserId);

        return draft.ToDto();
    }

    public async Task<EmailDraftDto> ApproveDraftAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        var draft = await _ownership.LoadOwnedAsync(draftId, localUserId, cancellationToken);

        if (draft.IsSent)
            throw new ConflictException("Cannot approve a draft that has already been sent.");

        if (string.IsNullOrWhiteSpace(draft.RecipientEmail))
            throw new ConflictException(
                "RecipientEmail must be set before approving. Please update the draft with the recruiter's email address.");

        if (!IsValidEmail(draft.RecipientEmail))
            throw new ConflictException(
                $"RecipientEmail '{draft.RecipientEmail}' is not a valid email address.");

        if (string.IsNullOrWhiteSpace(draft.Subject))
            throw new ConflictException("Subject must not be empty before approving.");

        if (string.IsNullOrWhiteSpace(draft.Body))
            throw new ConflictException("Body must not be empty before approving.");

        draft.IsApproved    = true;
        draft.ApprovedAtUtc = DateTime.UtcNow;
        draft.UpdatedAtUtc  = DateTime.UtcNow;

        await _emailDraftRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "EmailDraftService — draft {DraftId} approved by user {UserId}", draftId, localUserId);

        return draft.ToDto();
    }

    private static AgentEmailResponse BuildFallbackApplicationDraft(
        EmailContext ctx,
        string language)
    {
        var company = string.IsNullOrWhiteSpace(ctx.CompanyName) ? "votre entreprise" : ctx.CompanyName!;
        var title = string.IsNullOrWhiteSpace(ctx.JobTitle) ? "le poste" : ctx.JobTitle;
        var body = "Bonjour,\n\n" +
                   $"Je vous contacte pour vous transmettre ma candidature pour {title} chez {company}. " +
                   "Vous trouverez mon CV en piece jointe.\n\n" +
                   "Je reste a votre disposition pour un echange.\n\n" +
                   "Cordialement,";

        return new AgentEmailResponse
        {
            Subject = $"Application - {title}",
            Body = body,
            Language = string.IsNullOrWhiteSpace(language) ? "fr" : language,
            Tone = "professionnel"
        };
    }

    private static bool IsValidEmail(string email)
    {
        try   { var _ = new MailAddress(email); return true; }
        catch { return false; }
    }
}
