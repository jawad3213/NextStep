using System.Text.Json;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.data;
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
    private readonly IAgentHttpClient _agentHttpClient;
    private readonly SmtpEmailOptions _smtpOptions;
    private readonly AppDbContext _db;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        ICandidatureRepository candidatureRepository,
        ICvService cvService,
        IEmailDraftRepository emailDraftRepository,
        IAgentHttpClient agentHttpClient,
        IOptions<SmtpEmailOptions> smtpOptions,
        AppDbContext db,
        ILogger<EmailService> logger)
    {
        _candidatureRepository = candidatureRepository;
        _cvService = cvService;
        _emailDraftRepository = emailDraftRepository;
        _agentHttpClient = agentHttpClient;
        _smtpOptions = smtpOptions.Value;
        _db = db;
        _logger = logger;
    }

    public async Task<EmailDraftDto> GenerateDraftAsync(
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Load candidature ──────────────────────────────────────────────
        var candidature = await _candidatureRepository.GetByIdAsync(
            dto.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new KeyNotFoundException($"Candidature {dto.CandidatureId} not found.");

        // ── 2. Load user ─────────────────────────────────────────────────────
        var user = await _db.Utilisateurs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == candidature.IdUtilisateur, cancellationToken);

        if (user is null)
            throw new KeyNotFoundException($"User {candidature.IdUtilisateur} not found.");

        // ── 3. Load profile data ─────────────────────────────────────────────
        var userId = candidature.IdUtilisateur;

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

        // ── 4. Load offer ────────────────────────────────────────────────────
        var offre = await _db.OffresEmploi
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == candidature.IdOffre, cancellationToken);

        // ── 5. Parse offer analysis JSON ─────────────────────────────────────
        string? jobTitle = null;
        string? companyName = null;
        string? location = null;
        var requiredSkills = new List<string>();
        var preferredSkills = new List<string>();
        var missions = new List<string>();
        var requirements = new List<string>();

        if (offre?.AnalyseJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(offre.AnalyseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("job_title", out var jt))
                    jobTitle = jt.GetString();
                if (root.TryGetProperty("company_name", out var cn))
                    companyName = cn.GetString();
                if (root.TryGetProperty("location", out var loc))
                    location = loc.GetString();

                requiredSkills  = ExtractStringList(root, "required_skills");
                preferredSkills = ExtractStringList(root, "preferred_skills");
                missions        = ExtractStringList(root, "missions");
                requirements    = ExtractStringList(root, "requirements");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not parse AnalyseJson for offer {OfferId} — will use raw text only.",
                    candidature.IdOffre);
            }
        }

        // ── 6. Build Python request payload ──────────────────────────────────
        var pythonRequest = new
        {
            candidature_id = candidature.IdCandidature.ToString(),
            candidate = new
            {
                full_name        = $"{user.Prenom} {user.Nom}".Trim(),
                email            = user.Email,
                phone            = user.Telephone,
                current_title    = user.TitrePoste,
                skills           = skills,
                experiences      = experiences.Select(e =>
                    $"{e.Poste} chez {e.Entreprise}" +
                    $" ({e.DateDebut?.Year}\u2013{(e.DateFin.HasValue ? e.DateFin.Value.Year.ToString() : "présent")})").ToList(),
                education        = formations.Select(f =>
                    $"{f.Diplome} \u2013 {f.Etablissement} ({f.Annee})").ToList(),
                projects         = projets,
                certifications   = certifications,
            },
            job_offer = new
            {
                job_title        = jobTitle ?? "Poste non spécifié",
                company_name     = companyName,
                location         = location,
                required_skills  = requiredSkills,
                preferred_skills = preferredSkills,
                missions         = missions,
                requirements     = requirements,
                raw_text         = offre?.TexteBrut,
                analysis_json    = (object?)null,   // skip raw JSON blob to keep payload light
            },
            options = new
            {
                language                 = dto.Language,
                tone                     = dto.Tone,
                include_motivation_letter = dto.IncludeMotivationLetter,
            },
        };

        // ── 7. Call Python /email/generate ───────────────────────────────────
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

        // ── 8. Save draft ────────────────────────────────────────────────────
        var draft = new EmailDraft
        {
            CandidatureId  = candidature.IdCandidature,
            EmailType      = dto.EmailType,
            RecipientEmail = user.Email,
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

    public async Task<List<EmailDraftDto>> GetDraftsByCandidatureAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var drafts = await _emailDraftRepository.GetByCandidatureIdAsync(
            candidatureId, cancellationToken);

        return drafts.Select(MapToDto).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EmailDraftDto MapToDto(EmailDraft draft) => new()
    {
        Id             = draft.Id,
        CandidatureId  = draft.CandidatureId,
        EmailType      = draft.EmailType,
        RecipientEmail = draft.RecipientEmail,
        Subject        = draft.Subject,
        Body           = draft.Body,
        Language       = draft.Language,
        IsApproved     = draft.IsApproved,
        IsSent         = draft.IsSent,
        CreatedAtUtc   = draft.CreatedAtUtc,
        UpdatedAtUtc   = draft.UpdatedAtUtc,
        SentAtUtc      = draft.SentAtUtc,
        ErrorMessage   = draft.ErrorMessage,
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

    // ── Python response DTO  ────────────────────────────────────────
    private sealed class PythonEmailResponse
    {
        public string Subject  { get; set; } = string.Empty;
        public string Body     { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Tone     { get; set; } = string.Empty;
    }
}
