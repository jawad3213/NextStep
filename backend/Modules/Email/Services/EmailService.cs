using System.Net.Mail;
using System.Text.Json;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.data;
using NextStep.Shared.Config;
using NextStep.Modules.Candidature.Models;
using NextStep.Shared.Http;
using NextStep.Modules.Candidature.Repositories;
using NextStep.Modules.Cv.Services;
using NextStep.Modules.Email.DTOs;
using NextStep.Modules.Email.Models;
using NextStep.Modules.Email.Repositories;

namespace NextStep.Modules.Email.Services;

public class EmailService : IEmailService
{
    private readonly ICandidatureRepository _candidatureRepository;
    private readonly ICvService _cvService;
    private readonly IEmailDraftRepository _emailDraftRepository;
    private readonly IEmailSenderService _emailSenderService;
    private readonly IAgentHttpClient _agentHttpClient;
    private readonly SmtpEmailOptions _smtpOptions;
    private readonly AppDbContext _db;
    private readonly ILogger<EmailService> _logger;
    private readonly EmailFollowUpOptions _followUpOptions;

    public EmailService(
        ICandidatureRepository candidatureRepository,
        ICvService cvService,
        IEmailDraftRepository emailDraftRepository,
        IEmailSenderService emailSenderService,
        IAgentHttpClient agentHttpClient,
        IOptions<SmtpEmailOptions> smtpOptions,
        AppDbContext db,
        ILogger<EmailService> logger,
        IOptions<EmailFollowUpOptions> followUpOptions)
    {
        _candidatureRepository = candidatureRepository;
        _emailDraftRepository  = emailDraftRepository;
        _emailSenderService    = emailSenderService;
        _agentHttpClient       = agentHttpClient;
        _db                    = db;
        _logger                = logger;
        _followUpOptions       = followUpOptions.Value;
        _cvService = cvService;
        _smtpOptions = smtpOptions.Value;
    }

    // ── GenerateDraftAsync ────────────────────────────────────────────────────────

    public async Task<EmailDraftDto> GenerateDraftAsync(
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default)
    {
        var candidature = await _candidatureRepository.GetByIdAsync(
            dto.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new KeyNotFoundException($"Candidature {dto.CandidatureId} not found.");

        var ctx = await BuildCandidatureContextAsync(candidature.IdUtilisateur, candidature.IdOffre, cancellationToken);

        var pythonRequest = new
        {
            candidature_id = candidature.IdCandidature.ToString(),
            candidate      = BuildCandidatePayload(ctx),
            job_offer      = BuildJobOfferPayload(ctx),
            options = new
            {
                language                  = dto.Language,
                tone                      = dto.Tone,
                include_motivation_letter = dto.IncludeMotivationLetter,
            },
            skill_gap            = ctx.SkillGap,
            company_intelligence = ctx.CompanyIntelligence
        };

        _logger.LogInformation(
            "EmailService — calling Python /email/generate for candidature {CandidatureId}",
            dto.CandidatureId);

        PythonEmailResponse pythonResponse;
        try
        {
            pythonResponse = await _agentHttpClient
                .PostAsync<object, PythonEmailResponse>(
                    "/email/generate",
                    pythonRequest,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "EmailService — Python agent failed for candidature {CandidatureId}",
                dto.CandidatureId);
            throw new InvalidOperationException(
                $"Email generation failed: {ex.Message}", ex);
        }

        var draft = new EmailDraft
        {
            CandidatureId  = candidature.IdCandidature,
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

        _logger.LogInformation(
            "EmailService — draft saved for candidature {CandidatureId} | subject: {Subject}",
            dto.CandidatureId, draft.Subject);

        return MapToDto(draft);
    }

    // ── GetDraftByIdAsync ─────────────────────────────────────────────────────────

    public async Task<EmailDraftDto> GetDraftByIdAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        // LoadAndVerifyOwnershipAsync throws KeyNotFoundException / UnauthorizedAccessException
        var draft = await LoadAndVerifyOwnershipAsync(draftId, localUserId, cancellationToken);
        return MapToDto(draft);
    }

    // ── GetDraftsByCandidatureAsync ───────────────────────────────────────────────

    public async Task<List<EmailDraftDto>> GetDraftsByCandidatureAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var drafts = await _emailDraftRepository.GetByCandidatureIdAsync(
            candidatureId, cancellationToken);

        return drafts.Select(MapToDto).ToList();
    }

    // ── UpdateDraftAsync ──────────────────────────────────────────────────────────

    public async Task<EmailDraftDto> UpdateDraftAsync(
        Guid draftId,
        Guid localUserId,
        UpdateEmailDraftDto dto,
        CancellationToken cancellationToken = default)
    {
        var draft = await LoadAndVerifyOwnershipAsync(draftId, localUserId, cancellationToken);

        if (draft.IsSent)
            throw new InvalidOperationException("Cannot update a draft that has already been sent.");

        if (dto.RecipientEmail is not null)
            draft.RecipientEmail = dto.RecipientEmail.Trim();

        if (dto.Subject is not null)
            draft.Subject = dto.Subject.Trim();

        if (dto.Body is not null)
            draft.Body = dto.Body;

        draft.UpdatedAtUtc = DateTime.UtcNow;

        await _emailDraftRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "EmailService — draft {DraftId} updated by user {UserId}", draftId, localUserId);

        return MapToDto(draft);
    }

    // ── ApproveDraftAsync ─────────────────────────────────────────────────────────

    public async Task<EmailDraftDto> ApproveDraftAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        var draft = await LoadAndVerifyOwnershipAsync(draftId, localUserId, cancellationToken);

        if (draft.IsSent)
            throw new InvalidOperationException("Cannot approve a draft that has already been sent.");

        if (string.IsNullOrWhiteSpace(draft.RecipientEmail))
            throw new InvalidOperationException(
                "RecipientEmail must be set before approving. Please update the draft with the recruiter's email address.");

        if (!IsValidEmail(draft.RecipientEmail))
            throw new InvalidOperationException(
                $"RecipientEmail '{draft.RecipientEmail}' is not a valid email address.");

        if (string.IsNullOrWhiteSpace(draft.Subject))
            throw new InvalidOperationException("Subject must not be empty before approving.");

        if (string.IsNullOrWhiteSpace(draft.Body))
            throw new InvalidOperationException("Body must not be empty before approving.");

        draft.IsApproved    = true;
        draft.ApprovedAtUtc = DateTime.UtcNow;
        draft.UpdatedAtUtc  = DateTime.UtcNow;

        await _emailDraftRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "EmailService — draft {DraftId} approved by user {UserId}", draftId, localUserId);

        return MapToDto(draft);
    }

    // ── SendDraftAsync ────────────────────────────────────────────────────────────

    public async Task<SendEmailResultDto> SendDraftAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        var draft = await LoadAndVerifyOwnershipAsync(draftId, localUserId, cancellationToken);

        if (draft.IsSent)
            throw new InvalidOperationException("This draft has already been sent.");

        if (!draft.IsApproved)
            throw new InvalidOperationException(
                "Draft must be approved before sending. Please approve the draft first.");

        if (string.IsNullOrWhiteSpace(draft.RecipientEmail))
            throw new InvalidOperationException("RecipientEmail must be set before sending.");

        if (!IsValidEmail(draft.RecipientEmail))
            throw new InvalidOperationException(
                $"RecipientEmail '{draft.RecipientEmail}' is not a valid email address.");

        if (string.IsNullOrWhiteSpace(draft.Subject))
            throw new InvalidOperationException("Subject must not be empty before sending.");

        if (string.IsNullOrWhiteSpace(draft.Body))
            throw new InvalidOperationException("Body must not be empty before sending.");

        draft.SendAttemptCount++;
        draft.UpdatedAtUtc = DateTime.UtcNow;

        var result = await _emailSenderService.SendAsync(
            localUserId:       localUserId,
            recipientEmail:    draft.RecipientEmail,
            subject:           draft.Subject,
            body:              draft.Body,
            cancellationToken: cancellationToken);

        if (result.Success)
        {
            draft.IsSent            = true;
            draft.SentAtUtc         = DateTime.UtcNow;
            draft.ProviderMessageId = result.ProviderMessageId;
            draft.ProviderThreadId  = result.ProviderThreadId;
            draft.ErrorMessage      = null;

            _logger.LogInformation(
                "EmailService — draft {DraftId} sent for user {UserId}, Gmail message id: {GmailId}, thread id: {ThreadId}",
                draftId, localUserId, result.ProviderMessageId, result.ProviderThreadId);

            // ── Update candidature status if this was a relance ──────────────
            if (draft.EmailType == "relance")
            {
                // We load without AsNoTracking to ensure status update is saved
                var candidature = await _db.Candidatures
                    .FirstOrDefaultAsync(c => c.IdCandidature == draft.CandidatureId, cancellationToken);
                if (candidature is not null)
                {
                    candidature.ResponseStatus = "RELANCE_ENVOYEE";
                    candidature.Statut         = "RELANCE_ENVOYEE";
                    _logger.LogInformation(
                        "EmailService — candidature {CandidatureId} status updated to RELANCE_ENVOYEE",
                        draft.CandidatureId);
                }
            }
        }
        else
        {
            draft.IsSent       = false;
            draft.ErrorMessage = result.ErrorMessage;

            _logger.LogWarning(
                "EmailService — send failed for draft {DraftId}, user {UserId}: {Error}",
                draftId, localUserId, result.ErrorMessage);
        }

        await _emailDraftRepository.SaveChangesAsync(cancellationToken);

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

    // ── GenerateFollowUpDraftAsync ────────────────────────────────────────────────

    public async Task<EmailDraftDto> GenerateFollowUpDraftAsync(
        GenerateFollowUpDraftDto dto,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Load and verify candidature (with tracking) ───────────────────
        var candidature = await _db.Candidatures
            .FirstOrDefaultAsync(c => c.IdCandidature == dto.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new KeyNotFoundException($"Candidature {dto.CandidatureId} not found.");

        if (candidature.IdUtilisateur != localUserId)
            throw new UnauthorizedAccessException(
                $"User {localUserId} does not own candidature {dto.CandidatureId}.");

        // ── 2. Block if a reply was already received ─────────────────────────
        if (candidature.HasResponse)
            throw new InvalidOperationException(
                "A response has already been received for this candidature. Follow-up is not needed.");

        // ── 3. Check MaxFollowUps limit ──────────────────────────────────────
        int maxFollowUps = _followUpOptions.MaxFollowUps;

        int sentRelanceCount = await _db.EmailDrafts
            .CountAsync(d =>
                d.CandidatureId == candidature.IdCandidature &&
                d.EmailType == "relance" &&
                d.IsSent,
                cancellationToken);

        if (sentRelanceCount >= maxFollowUps)
            throw new InvalidOperationException(
                $"Maximum number of follow-ups ({maxFollowUps}) reached for this candidature.");

        // ── 4. Return existing unsent relance draft if one already exists ────
        var existingUnsentRelance = await _db.EmailDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.CandidatureId == candidature.IdCandidature &&
                d.EmailType == "relance" &&
                !d.IsSent,
                cancellationToken);

        if (existingUnsentRelance is not null)
        {
            _logger.LogWarning(
                "EmailService — unsent relance draft {DraftId} already exists for candidature {CandidatureId}. Returning existing draft.",
                existingUnsentRelance.Id, dto.CandidatureId);
            return MapToDto(existingUnsentRelance);
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
                    d.CandidatureId == candidature.IdCandidature &&
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
                    d.CandidatureId == candidature.IdCandidature &&
                    d.IsSent &&
                    d.SentAtUtc != null)
                .OrderByDescending(d => d.SentAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (previousDraft is null)
            throw new InvalidOperationException(
                "Cannot generate a follow-up because no sent email exists for this candidature.");

        int daysSinceSent = (int)(DateTime.UtcNow - previousDraft.SentAtUtc!.Value).TotalDays;

        // ── 6. Load profile and offer context ────────────────────────────────
        var ctx = await BuildCandidatureContextAsync(
            candidature.IdUtilisateur, candidature.IdOffre, cancellationToken);

        // ── 7. Build Python payload ──────────────────────────────────────────
        var pythonRequest = new
        {
            candidature_id = candidature.IdCandidature.ToString(),
            candidate      = BuildCandidatePayload(ctx),
            job_offer      = BuildJobOfferPayload(ctx),
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
            "EmailService — calling Python /email/generate-follow-up for candidature {CandidatureId} | sentRelances={Count} | daysSinceSent={Days}",
            dto.CandidatureId, sentRelanceCount, daysSinceSent);

        PythonEmailResponse pythonResponse;
        try
        {
            pythonResponse = await _agentHttpClient
                .PostAsync<object, PythonEmailResponse>(
                    "/email/generate-follow-up",
                    pythonRequest,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "EmailService — Python follow-up agent failed for candidature {CandidatureId}",
                dto.CandidatureId);
            throw new InvalidOperationException(
                $"Follow-up email generation failed: {ex.Message}", ex);
        }

        // ── 8. Save relance draft ────────────────────────────────────────────
        var draft = new EmailDraft
        {
            CandidatureId  = candidature.IdCandidature,
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

        // ── 9. Update candidature status ─────────────────────────────────────
        candidature.ResponseStatus = "RELANCE_GENEREE";
        candidature.Statut         = "RELANCE_GENEREE";

        await _emailDraftRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "EmailService — relance draft {DraftId} saved for candidature {CandidatureId} | subject: {Subject}",
            draft.Id, dto.CandidatureId, draft.Subject);

        return MapToDto(draft);
    }

    // ── GenerateReplyDraftAsync ───────────────────────────────────────────────────

    public async Task<EmailDraftDto> GenerateReplyDraftAsync(
        GenerateReplyDraftDto dto,
        Guid localUserId,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Load and verify candidature ───────────────────────────────────
        var candidature = await _db.Candidatures
            .FirstOrDefaultAsync(c => c.IdCandidature == dto.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new KeyNotFoundException($"Candidature {dto.CandidatureId} not found.");

        if (candidature.IdUtilisateur != localUserId)
            throw new UnauthorizedAccessException(
                $"User {localUserId} does not own candidature {dto.CandidatureId}.");

        // ── 2. Guard: reply must have been detected ───────────────────────────
        if (!candidature.HasResponse)
            throw new InvalidOperationException(
                "Cannot generate reply draft because no recruiter response has been detected for this candidature.");

        // ── 3. Guard: need some reply context ────────────────────────────────
        if (string.IsNullOrWhiteSpace(candidature.LastResponseSnippet) &&
            string.IsNullOrWhiteSpace(candidature.ResponseSummary))
            throw new InvalidOperationException(
                "Cannot generate reply draft because the recruiter response context (snippet/summary) is missing.");

        // ── 4. Duplicate prevention: return existing unsent reply draft ──────
        var existingUnsentReply = await _db.EmailDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.CandidatureId == candidature.IdCandidature &&
                d.EmailType     == "reply" &&
                !d.IsSent,
                cancellationToken);

        if (existingUnsentReply is not null)
        {
            _logger.LogWarning(
                "EmailService — unsent reply draft {DraftId} already exists for candidature {CandidatureId}. Returning existing draft.",
                existingUnsentReply.Id, dto.CandidatureId);
            return MapToDto(existingUnsentReply);
        }

        // ── 5. Find previous sent email for context ───────────────────────────
        var previousDraft = await _db.EmailDrafts
            .AsNoTracking()
            .Where(d =>
                d.CandidatureId == candidature.IdCandidature &&
                (d.EmailType == "application" || d.EmailType == "relance") &&
                d.IsSent &&
                d.SentAtUtc != null)
            .OrderByDescending(d => d.SentAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // ── 6. Load candidate + offer context ────────────────────────────────
        var ctx = await BuildCandidatureContextAsync(
            candidature.IdUtilisateur, candidature.IdOffre, cancellationToken);

        // ── 7. Resolve recipient email ────────────────────────────────────────
        // Prefer LastResponseFrom (parse "Name <email>" format safely).
        // Fallback to previousDraft.RecipientEmail. Null if neither is valid.
        string? recipientEmail = TryParseEmailAddress(candidature.LastResponseFrom)
                                 ?? previousDraft?.RecipientEmail;

        // ── 8. Build Python payload ──────────────────────────────────────────
        var pythonRequest = new
        {
            candidature_id = candidature.IdCandidature.ToString(),
            candidate      = BuildCandidatePayload(ctx),
            job_offer      = BuildJobOfferPayload(ctx),
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
            "EmailService — calling Python /email/generate-reply for candidature {CandidatureId} | responseType={ResponseType}",
            dto.CandidatureId, candidature.ResponseStatus);

        // ── 9. Call Python agent ──────────────────────────────────────────────
        PythonEmailResponse pythonResponse;
        try
        {
            pythonResponse = await _agentHttpClient
                .PostAsync<object, PythonEmailResponse>(
                    "/email/generate-reply",
                    pythonRequest,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "EmailService — Python reply agent failed for candidature {CandidatureId}",
                dto.CandidatureId);
            throw new InvalidOperationException(
                $"Reply email generation failed: {ex.Message}", ex);
        }

        // ── 10. Save reply draft ─────────────────────────────────────────────
        // IMPORTANT: Do NOT modify candidature.ResponseStatus or candidature.Statut here.
        // Classification information must remain visible to the user.
        var draft = new EmailDraft
        {
            CandidatureId  = candidature.IdCandidature,
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
            "EmailService — reply draft {DraftId} saved for candidature {CandidatureId} | subject: {Subject} | recipient: {Recipient}",
            draft.Id, dto.CandidatureId, draft.Subject, recipientEmail ?? "(none — user must set)");

        return MapToDto(draft);
    }

    // ── Private: profile + offer context builder ──────────────────────────────────

    private sealed record CandidatureContext(
        string FullName,
        string? Email,
        string? Phone,
        string? CurrentTitle,
        List<string> Skills,
        List<string> Experiences,
        List<string> Education,
        List<string> Projects,
        List<string> Certifications,
        string JobTitle,
        string? CompanyName,
        string? Location,
        List<string> RequiredSkills,
        List<string> PreferredSkills,
        List<string> Missions,
        List<string> Requirements,
        string? RawText,
        JsonElement? SkillGap = null,
        JsonElement? CompanyIntelligence = null);

    private async Task<CandidatureContext> BuildCandidatureContextAsync(
        Guid userId,
        Guid offreId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Utilisateurs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        var skills = await _db.Competences
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.Nom ?? "")
            .Where(n => n.Length > 0)
            .ToListAsync(cancellationToken);

        var experiences = await _db.Experiences
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        var formations = await _db.Formations
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .ToListAsync(cancellationToken);

        var projets = await _db.Projets
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.TitreProjet ?? "")
            .Where(t => t.Length > 0)
            .ToListAsync(cancellationToken);

        var certifications = await _db.Certifications
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.Titre ?? "")
            .Where(t => t.Length > 0)
            .ToListAsync(cancellationToken);

        var offre = await _db.OffresEmploi
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == offreId, cancellationToken);

        string? jobTitle = null;
        string? companyName = null;
        string? location = null;
        var requiredSkills  = new List<string>();
        var preferredSkills = new List<string>();
        var missions        = new List<string>();
        var requirements    = new List<string>();
        JsonElement? skillGap = null;
        JsonElement? companyIntelligence = null;

        if (offre?.AnalyseJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(offre.AnalyseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("job_title", out var jt))   jobTitle    = jt.GetString();
                if (root.TryGetProperty("company_name", out var cn)) companyName = cn.GetString();
                if (root.TryGetProperty("location", out var loc))    location    = loc.GetString();

                requiredSkills  = ExtractStringList(root, "required_skills");
                preferredSkills = ExtractStringList(root, "preferred_skills");
                missions        = ExtractStringList(root, "missions");
                requirements    = ExtractStringList(root, "requirements");

                // Extract enrichment if the full pipeline JSON was saved
                if (root.TryGetProperty("match_result", out var mr)) skillGap = mr;
                if (root.TryGetProperty("company_intelligence", out var ci)) companyIntelligence = ci;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not parse AnalyseJson for offer {OfferId} — will use raw text only.", offreId);
            }
        }

        return new CandidatureContext(
            FullName:        $"{user.Prenom} {user.Nom}".Trim(),
            Email:           user.Email,
            Phone:           user.Telephone,
            CurrentTitle:    user.TitrePoste,
            Skills:          skills,
            Experiences:     experiences.Select(e =>
                $"{e.Poste} chez {e.Entreprise}" +
                $" ({e.DateDebut?.Year}\u2013{(e.DateFin.HasValue ? e.DateFin.Value.Year.ToString() : "présent")})").ToList(),
            Education:       formations.Select(f =>
                $"{f.Diplome} \u2013 {f.Etablissement} ({f.Annee})").ToList(),
            Projects:        projets,
            Certifications:  certifications,
            JobTitle:        jobTitle ?? "Poste non spécifié",
            CompanyName:     companyName,
            Location:        location,
            RequiredSkills:  requiredSkills,
            PreferredSkills: preferredSkills,
            Missions:        missions,
            Requirements:    requirements,
            RawText:         offre?.TexteBrut,
            SkillGap:        skillGap,
            CompanyIntelligence: companyIntelligence);
    }

    private static object BuildCandidatePayload(CandidatureContext ctx) => new
    {
        full_name     = ctx.FullName,
        email         = ctx.Email,
        phone         = ctx.Phone,
        current_title = ctx.CurrentTitle,
        skills        = ctx.Skills,
        experiences   = ctx.Experiences,
        education     = ctx.Education,
        projects      = ctx.Projects,
        certifications = ctx.Certifications,
    };

    private static object BuildJobOfferPayload(CandidatureContext ctx) => new
    {
        job_title        = ctx.JobTitle,
        company_name     = ctx.CompanyName,
        location         = ctx.Location,
        required_skills  = ctx.RequiredSkills,
        preferred_skills = ctx.PreferredSkills,
        missions         = ctx.Missions,
        requirements     = ctx.Requirements,
        raw_text         = ctx.RawText,
        analysis_json    = (object?)null,
    };

    // ── Private: load + ownership check ──────────────────────────────────────────

    private async Task<EmailDraft> LoadAndVerifyOwnershipAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken)
    {
        var draft = await _db.EmailDrafts
            .FirstOrDefaultAsync(d => d.Id == draftId, cancellationToken);

        if (draft is null)
            throw new KeyNotFoundException($"Email draft {draftId} not found.");

        var candidature = await _candidatureRepository.GetByIdAsync(
            draft.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new KeyNotFoundException(
                $"Candidature {draft.CandidatureId} not found for draft {draftId}.");

        if (candidature.IdUtilisateur != localUserId)
            throw new UnauthorizedAccessException(
                $"User {localUserId} does not own draft {draftId}.");

        return draft;
    }

    private static bool IsValidEmail(string email)
    {
        try   { var _ = new MailAddress(email); return true; }
        catch { return false; }
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

    public async Task<EmailDraftDto> SendApplicationEmailAsync(
        Guid userId,
        SendApplicationEmailDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RecipientEmail))
            throw new InvalidOperationException("Recipient email is required.");
        if (string.IsNullOrWhiteSpace(dto.Subject))
            throw new InvalidOperationException("Email subject is required.");
        if (string.IsNullOrWhiteSpace(dto.Body))
            throw new InvalidOperationException("Email body is required.");

        var candidature = await _db.Candidatures
            .FirstOrDefaultAsync(c => c.IdOffre == dto.OfferId && c.IdUtilisateur == userId, cancellationToken);

        if (candidature is null)
            throw new KeyNotFoundException($"No candidature found for offer {dto.OfferId}.");

        var cvHistoryId = dto.CvHistoryId ?? await _db.CvHistories
            .Where(h => h.UserId == userId && h.Title == $"CV_{dto.OfferId}")
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => (Guid?)h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!cvHistoryId.HasValue)
            throw new InvalidOperationException("No final CV was found to attach to the email.");

        var attachmentBytes = await _cvService.GetDownloadBytesAsync(userId, cvHistoryId.Value);
        var draft = new EmailDraft
        {
            CandidatureId = candidature.IdCandidature,
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
            await SendEmailMessageAsync(draft, dto.OfferId, attachmentBytes, cancellationToken);
            draft.IsSent = true;
            draft.SentAtUtc = DateTime.UtcNow;
            draft.UpdatedAtUtc = draft.SentAtUtc;
            draft.ErrorMessage = null;
        }
        catch (Exception ex)
        {
            draft.IsSent = false;
            draft.UpdatedAtUtc = DateTime.UtcNow;
            draft.ErrorMessage = ex.Message;
            _logger.LogError(ex, "EmailService - failed to send application email for offer {OfferId}", dto.OfferId);
        }

        await _emailDraftRepository.AddAsync(draft, cancellationToken);

        if (!draft.IsSent)
            throw new InvalidOperationException(draft.ErrorMessage ?? "Email sending failed.");

        return MapToDto(draft);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EmailDraftDto MapToDto(EmailDraft draft) => new()
    {
        Id                = draft.Id,
        CandidatureId     = draft.CandidatureId,
        EmailType         = draft.EmailType,
        RecipientEmail    = draft.RecipientEmail,
        Subject           = draft.Subject,
        Body              = draft.Body,
        Language          = draft.Language,
        IsApproved        = draft.IsApproved,
        IsSent            = draft.IsSent,
        CreatedAtUtc      = draft.CreatedAtUtc,
        UpdatedAtUtc      = draft.UpdatedAtUtc,
        ApprovedAtUtc     = draft.ApprovedAtUtc,
        SentAtUtc         = draft.SentAtUtc,
        ErrorMessage      = draft.ErrorMessage,
        ProviderMessageId = draft.ProviderMessageId,
        ProviderThreadId  = draft.ProviderThreadId,
        SendAttemptCount  = draft.SendAttemptCount,
    };

    private static List<string> ExtractStringList(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            return arr.EnumerateArray()
                      .Select(x => x.GetString() ?? "")
                      .Where(s => s.Length > 0)
                      .ToList();
        }
        return [];
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
            throw new InvalidOperationException("SMTP is not configured. Please set Email:Smtp:Host and Email:Smtp:FromEmail.");
        }
    }

    // ── Python response DTO (internal) ────────────────────────────────────────────
    private sealed class PythonEmailResponse
    {
        public string Subject  { get; set; } = string.Empty;
        public string Body     { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Tone     { get; set; } = string.Empty;
    }
}
