using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextStep.data;
using NextStep.Shared.Http;
using NextStep.Modules.Candidature.Repositories;
using NextStep.Modules.Email.DTOs;
using NextStep.Modules.Email.Models;
using NextStep.Modules.Email.Repositories;

namespace NextStep.Modules.Email.Services;

public class EmailService : IEmailService
{
    private readonly ICandidatureRepository _candidatureRepository;
    private readonly IEmailDraftRepository _emailDraftRepository;
    private readonly IAgentHttpClient _agentHttpClient;
    private readonly AppDbContext _db;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        ICandidatureRepository candidatureRepository,
        IEmailDraftRepository emailDraftRepository,
        IAgentHttpClient agentHttpClient,
        AppDbContext db,
        ILogger<EmailService> logger)
    {
        _candidatureRepository = candidatureRepository;
        _emailDraftRepository = emailDraftRepository;
        _agentHttpClient = agentHttpClient;
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

    // ── Python response DTO  ────────────────────────────────────────
    private sealed class PythonEmailResponse
    {
        public string Subject  { get; set; } = string.Empty;
        public string Body     { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Tone     { get; set; } = string.Empty;
    }
}