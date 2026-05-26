using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.data;
using NextStep.Modules.Candidature.Models;
using NextStep.Modules.Cv.Models;
using NextStep.Modules.Cv.Templates;
using NextStep.Modules.Offer.Services;
using NextStep.Modules.Profile.Services;
using NextStep.Shared.Http;
using NextStep.Shared.Storage;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NextStep.Modules.Cv.Services;

public interface ICvService
{
    Task<CvPreviewResult> PreviewCvAsync(Guid userId, string templateId, Guid? jobId = null);
    Task<CvRenderResponse> PreviewFromDataAsync(CvRenderRequest request);
    Task<byte[]> ExportPdfAsync(CvExportPdfRequest request);
    Task<CvSaveResult> SaveCvAsync(Guid userId, CvSaveRequest request);
    Task<CvSaveResult> UpdateCvAsync(Guid userId, Guid historyId, CvSaveRequest request);
    Task<List<CvHistoryDto>> GetHistoryAsync(Guid userId);
    Task<CvLoadResult> LoadCvAsync(Guid userId, Guid historyId);
    Task<string> GetDownloadUrlAsync(Guid userId, Guid historyId);
    Task<byte[]> GetDownloadBytesAsync(Guid userId, Guid historyId);
    Task DeleteCvAsync(Guid userId, Guid historyId);
}

public class CvPreviewResult
{
    public string TemplateSlug { get; set; } = string.Empty;
    public CvData Data { get; set; } = new();
    public CvDesignConfig DesignConfig { get; set; } = new();
    public string Html { get; set; } = string.Empty;
}

public class CvSaveRequest
{
    public string TemplateSlug { get; set; } = string.Empty;
    public string? Title { get; set; }
    public Guid? OfferId { get; set; }
    public CvData Data { get; set; } = new();
    public CvDesignConfig? DesignConfig { get; set; }
    public string? HtmlSnapshot { get; set; }
}

public class CvSaveResult
{
    public Guid HistoryId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public class CvLoadResult
{
    public Guid HistoryId { get; set; }
    public string TemplateSlug { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string? Title { get; set; }
    public CvData Data { get; set; } = new();
    public CvDesignConfig DesignConfig { get; set; } = new();
    public string? HtmlSnapshot { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CvHistoryDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string TemplateSlug { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CvService : ICvService
{
    private readonly IProfileService _profileService;
    private readonly IOfferService _offerService;
    private readonly IStorageService _storageService;
    private readonly MinioOptions _minioOptions;
    private readonly AppDbContext _db;
    private readonly IAgentHttpClient _agentClient;
    private readonly ICvHtmlTemplateRenderer _htmlTemplateRenderer;
    private readonly ICvPdfRenderer _pdfRenderer;

    private static readonly string[] ActivitySignals =
    {
        "hackathon", "club", "association", "organisateur", "organizer",
        "membre", "member", "volunteer", "benevole", "event", "community",
        "it day", "prize", "prix", "participant", "formateur", "trainer",
        "formation", "solihackathon", "itwave", "ids"
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public CvService(
        IProfileService profileService,
        IOfferService offerService,
        IStorageService storageService,
        IOptions<MinioOptions> minioOptions,
        AppDbContext db,
        IAgentHttpClient agentClient,
        ICvHtmlTemplateRenderer htmlTemplateRenderer,
        ICvPdfRenderer pdfRenderer)
    {
        _profileService = profileService;
        _offerService = offerService;
        _storageService = storageService;
        _minioOptions = minioOptions.Value;
        _db = db;
        _agentClient = agentClient;
        _htmlTemplateRenderer = htmlTemplateRenderer;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<CvPreviewResult> PreviewCvAsync(Guid userId, string templateId, Guid? jobId = null)
    {
        object? offerData = null;
        if (jobId.HasValue)
        {
            var analysis = await _offerService.GetAnalysisAsync(userId, jobId.Value);
            if (analysis != null)
            {
                offerData = new
                {
                    titre = analysis.Titre,
                    description = analysis.DescriptionPoste,
                    competences_requises = analysis.CompetencesRequises,
                    keywords_ats = analysis.KeywordsAts
                };
            }
        }

        var request = new
        {
            user_id = userId.ToString(),
            template_slug = templateId,
            offer_data = offerData
        };

        var response = await _agentClient.PostAsync<object, CvEngineResult>("/prepare-cv", request);

        var data = SanitizeCvData(response.CvJson ?? new CvData());
        data.ThemeColor = null;

        if (jobId.HasValue && jobId.Value != Guid.Empty)
        {
            var candidature = await _db.Candidatures
                .FirstOrDefaultAsync(c => c.IdUtilisateur == userId && c.IdOffre == jobId.Value);

            if (candidature != null)
            {
                var docGenere = await _db.DocumentsGeneres
                    .FirstOrDefaultAsync(d => d.IdCandidature == candidature.IdCandidature);

                var jsonString = JsonSerializer.Serialize(data, _jsonOptions);

                if (docGenere != null)
                {
                    docGenere.CvContenuIaJson = jsonString;
                    docGenere.DateGeneration = DateTime.UtcNow;
                    docGenere.Version += 1;
                }
                else
                {
                    docGenere = new DocumentGenere
                    {
                        IdCandidature = candidature.IdCandidature,
                        CvContenuIaJson = jsonString,
                        Version = 1,
                        DateGeneration = DateTime.UtcNow
                    };
                    _db.DocumentsGeneres.Add(docGenere);
                }

                await _db.SaveChangesAsync();
            }
        }

        var renderResult = await _htmlTemplateRenderer.RenderAsync(templateId, data);

        return new CvPreviewResult
        {
            TemplateSlug = renderResult.TemplateSlug,
            Data = data,
            DesignConfig = renderResult.DesignConfig,
            Html = renderResult.Html,
        };
    }

    public async Task<CvRenderResponse> PreviewFromDataAsync(CvRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Data = SanitizeCvData(request.Data);
        return await _htmlTemplateRenderer.RenderAsync(request.TemplateSlug, request.Data, request.DesignConfig);
    }

    public async Task<byte[]> ExportPdfAsync(CvExportPdfRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Data = SanitizeCvData(request.Data);

        var html = string.IsNullOrWhiteSpace(request.HtmlSnapshot)
            ? (await _htmlTemplateRenderer.RenderAsync(request.TemplateSlug, request.Data, request.DesignConfig)).Html
            : request.HtmlSnapshot!;

        return await RenderPdfWithFallbackAsync(request.TemplateSlug, request.Data, html);
    }

    public async Task<CvSaveResult> SaveCvAsync(Guid userId, CvSaveRequest request)
    {
        request.Data = SanitizeCvData(request.Data);
        var renderResult = await _htmlTemplateRenderer.RenderAsync(request.TemplateSlug, request.Data, request.DesignConfig);
        var htmlSnapshot = request.HtmlSnapshot ?? renderResult.Html;
        var pdfBytes = await RenderPdfWithFallbackAsync(renderResult.TemplateSlug, request.Data, htmlSnapshot);

        var objectKey = $"cvs/{userId}/{Guid.NewGuid()}.pdf";
        var fileUrl = await _storageService.UploadFileAsync(objectKey, pdfBytes, "application/pdf");

        var history = new CvHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            TemplateSlug = renderResult.TemplateSlug,
            TemplateName = renderResult.TemplateSlug,
            CvDataJson = JsonSerializer.Serialize(request.Data, _jsonOptions),
            DesignConfigJson = JsonSerializer.Serialize(renderResult.DesignConfig, _jsonOptions),
            HtmlSnapshot = htmlSnapshot,
            FileUrl = fileUrl,
            ObjectKey = objectKey,
            BucketName = _minioOptions.BucketName,
            FileSizeBytes = pdfBytes.Length,
            CreatedAt = DateTime.UtcNow,
        };

        _db.CvHistories.Add(history);
        await _db.SaveChangesAsync();

        if (request.OfferId.HasValue && request.OfferId.Value != Guid.Empty)
        {
            var offer = await _db.OffresEmploi
                .FirstOrDefaultAsync(o => o.Id == request.OfferId.Value && o.UtilisateurId == userId);

            if (offer is not null)
            {
                var candidature = await _db.Candidatures
                    .FirstOrDefaultAsync(c => c.IdOffre == request.OfferId.Value && c.IdUtilisateur == userId);

                if (candidature == null)
                {
                    candidature = new NextStep.Modules.Candidature.Models.Candidature
                    {
                        IdUtilisateur = userId,
                        IdOffre = request.OfferId.Value,
                        Offre = offer,
                        Statut = "EN_ATTENTE",
                        DateCreation = DateTime.UtcNow
                    };
                    _db.Candidatures.Add(candidature);
                    await _db.SaveChangesAsync();
                }

                var document = await _db.DocumentsGeneres
                    .FirstOrDefaultAsync(d => d.IdCandidature == candidature.IdCandidature);

                var serializedCv = JsonSerializer.Serialize(request.Data, _jsonOptions);
                if (document == null)
                {
                    document = new DocumentGenere
                    {
                        IdCandidature = candidature.IdCandidature,
                        CvContenuIaJson = serializedCv,
                        CheminPdfCv = fileUrl,
                        Version = 1,
                        DateGeneration = DateTime.UtcNow
                    };
                    _db.DocumentsGeneres.Add(document);
                }
                else
                {
                    document.CvContenuIaJson = serializedCv;
                    document.CheminPdfCv = fileUrl;
                    document.Version += 1;
                    document.DateGeneration = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();
            }
        }

        return new CvSaveResult
        {
            HistoryId = history.Id,
            FileUrl = fileUrl,
            FileSizeBytes = pdfBytes.Length,
        };
    }

    public async Task<CvSaveResult> UpdateCvAsync(Guid userId, Guid historyId, CvSaveRequest request)
    {
        request.Data = SanitizeCvData(request.Data);
        var renderResult = await _htmlTemplateRenderer.RenderAsync(request.TemplateSlug, request.Data, request.DesignConfig);
        var htmlSnapshot = request.HtmlSnapshot ?? renderResult.Html;

        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        var pdfBytes = await RenderPdfWithFallbackAsync(renderResult.TemplateSlug, request.Data, htmlSnapshot);
        var objectKey = $"cvs/{userId}/{Guid.NewGuid()}.pdf";
        var fileUrl = await _storageService.UploadFileAsync(objectKey, pdfBytes, "application/pdf");

        history.Title = request.Title;
        history.TemplateSlug = renderResult.TemplateSlug;
        history.TemplateName = renderResult.TemplateSlug;
        history.CvDataJson = JsonSerializer.Serialize(request.Data, _jsonOptions);
        history.DesignConfigJson = JsonSerializer.Serialize(renderResult.DesignConfig, _jsonOptions);
        history.HtmlSnapshot = htmlSnapshot;
        history.FileUrl = fileUrl;
        history.ObjectKey = objectKey;
        history.FileSizeBytes = pdfBytes.Length;
        history.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new CvSaveResult
        {
            HistoryId = history.Id,
            FileUrl = fileUrl,
            FileSizeBytes = pdfBytes.Length,
        };
    }

    public async Task<List<CvHistoryDto>> GetHistoryAsync(Guid userId)
    {
        return await _db.CvHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new CvHistoryDto
            {
                Id = h.Id,
                Title = h.Title,
                TemplateSlug = h.TemplateSlug,
                TemplateName = h.TemplateName,
                FileUrl = h.FileUrl,
                FileSizeBytes = h.FileSizeBytes,
                CreatedAt = h.CreatedAt,
                UpdatedAt = h.UpdatedAt,
            })
            .ToListAsync();
    }

    public async Task<CvLoadResult> LoadCvAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        var data = SanitizeCvData(JsonSerializer.Deserialize<CvData>(history.CvDataJson, _jsonOptions) ?? new CvData());
        var designConfig = JsonSerializer.Deserialize<CvDesignConfig>(history.DesignConfigJson, _jsonOptions)
            ?? _htmlTemplateRenderer.GetDefaultDesignConfig(history.TemplateSlug);

        return new CvLoadResult
        {
            HistoryId = history.Id,
            TemplateSlug = history.TemplateSlug,
            TemplateName = history.TemplateName,
            Title = history.Title,
            Data = data,
            DesignConfig = designConfig,
            HtmlSnapshot = history.HtmlSnapshot,
            FileUrl = history.FileUrl,
            CreatedAt = history.CreatedAt,
            UpdatedAt = history.UpdatedAt,
        };
    }

    public async Task<string> GetDownloadUrlAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        return await _storageService.GetPresignedUrlAsync(history.ObjectKey, TimeSpan.FromHours(1));
    }

    public async Task<byte[]> GetDownloadBytesAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        return await _storageService.DownloadFileAsync(history.ObjectKey);
    }

    public async Task DeleteCvAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        _db.CvHistories.Remove(history);
        await _db.SaveChangesAsync();
    }

    public static CvData SanitizeCvData(CvData data)
    {
        data ??= new CvData();
        data.Experience ??= new List<CvExperience>();
        data.Education ??= new List<CvEducation>();
        data.Activities ??= new List<CvActivity>();
        data.Projects ??= new List<CvProject>();
        data.Skills ??= new List<CvSkill>();
        data.TechnicalSkills ??= new List<CvSkill>();
        data.SoftSkills ??= new List<CvSkill>();
        data.Certifications ??= new List<string>();
        data.Languages ??= new List<string>();
        data.Sections ??= new List<CvSection>();

        data.Skills = MergeSkillSources(data.Skills, data.TechnicalSkills, data.SoftSkills);
        data.Experience = NormalizeExperience(data.Experience, data.Activities);
        data.Activities = DeduplicateActivities(data.Activities);
        data.Projects = NormalizeProjects(data.Projects);
        data.Skills = DeduplicateSkills(data.Skills).ToList();
        data.Certifications = DeduplicateStrings(data.Certifications).ToList();
        data.Languages = DeduplicateStrings(data.Languages).ToList();

        data.Sections = CvSectionMapper.MergeWithLegacySections(data, data.Sections);
        return data;
    }

    private static List<CvExperience> NormalizeExperience(List<CvExperience>? experiences, List<CvActivity>? activities)
    {
        var clean = new List<CvExperience>();
        var seen = new HashSet<string>();
        activities ??= new List<CvActivity>();

        foreach (var exp in experiences ?? new List<CvExperience>())
        {
            exp.Role = CleanText(exp.Role);
            exp.Company = CleanText(exp.Company);
            exp.Start = NullIfEmpty(exp.Start);
            exp.End = NullIfEmpty(exp.End);
            exp.Bullets = DeduplicateStrings(exp.Bullets).ToList();

            if (string.IsNullOrWhiteSpace(exp.Role) && string.IsNullOrWhiteSpace(exp.Company))
                continue;

            if (LooksLikeActivity(exp))
            {
                activities.Add(new CvActivity
                {
                    Title = string.IsNullOrWhiteSpace(exp.Company) ? exp.Role : exp.Company,
                    Role = string.IsNullOrWhiteSpace(exp.Company) ? null : exp.Role,
                    Description = exp.Bullets.FirstOrDefault()
                });
                continue;
            }

            var key = NormalizeKey($"{exp.Role}|{exp.Company}");
            if (!seen.Add(key)) continue;
            clean.Add(exp);
        }

        return clean;
    }

    private static List<CvProject> NormalizeProjects(List<CvProject>? projects)
    {
        var clean = new List<CvProject>();
        var seen = new HashSet<string>();

        foreach (var project in projects ?? new List<CvProject>())
        {
            project.Title = CleanText(project.Title);
            project.Description = NullIfEmpty(project.Description);
            project.DateRealisation = NullIfEmpty(project.DateRealisation);
            project.Technologies = DeduplicateStrings(project.Technologies).ToList();
            project.Bullets = DeduplicateStrings(project.Bullets).ToList();

            if (string.IsNullOrWhiteSpace(project.Title))
                continue;

            var key = NormalizeKey(project.Title);
            if (!seen.Add(key)) continue;

            project.Bullets = project.Bullets
                .Where(b => !IsSameMeaning(b, project.Description))
                .ToList();

            clean.Add(project);
        }

        return clean;
    }

    private static List<CvActivity> DeduplicateActivities(List<CvActivity>? activities)
    {
        var clean = new List<CvActivity>();
        var seen = new HashSet<string>();

        foreach (var activity in activities ?? new List<CvActivity>())
        {
            activity.Title = CleanText(activity.Title);
            activity.Role = NullIfEmpty(activity.Role);
            activity.Description = NullIfEmpty(activity.Description);
            activity.StartDate = NullIfEmpty(activity.StartDate);
            activity.EndDate = NullIfEmpty(activity.EndDate);

            if (string.IsNullOrWhiteSpace(activity.Title) && string.IsNullOrWhiteSpace(activity.Role))
                continue;

            var key = NormalizeKey($"{activity.Role}|{activity.Title}");
            if (!seen.Add(key)) continue;
            clean.Add(activity);
        }

        return clean;
    }

    private static IEnumerable<string> DeduplicateStrings(IEnumerable<string>? values)
    {
        var seen = new HashSet<string>();
        foreach (var value in values ?? Enumerable.Empty<string>())
        {
            var clean = CleanText(value);
            if (string.IsNullOrWhiteSpace(clean)) continue;
            if (seen.Add(NormalizeKey(clean))) yield return clean;
        }
    }

    private static IEnumerable<CvSkill> DeduplicateSkills(IEnumerable<CvSkill>? skills)
    {
        var seen = new HashSet<string>();
        foreach (var skill in skills ?? Enumerable.Empty<CvSkill>())
        {
            skill.Name = CleanText(skill.Name);
            if (string.IsNullOrWhiteSpace(skill.Name)) continue;
            if (!seen.Add(NormalizeKey(skill.Name))) continue;
            skill.Level = Math.Clamp(skill.Level, 1, 5);
            yield return skill;
        }
    }

    private static List<CvSkill> MergeSkillSources(
        IEnumerable<CvSkill>? skills,
        IEnumerable<CvSkill>? technicalSkills,
        IEnumerable<CvSkill>? softSkills)
    {
        var merged = new List<CvSkill>();
        merged.AddRange(skills ?? Enumerable.Empty<CvSkill>());
        merged.AddRange(technicalSkills ?? Enumerable.Empty<CvSkill>());

        foreach (var skill in softSkills ?? Enumerable.Empty<CvSkill>())
        {
            if (string.IsNullOrWhiteSpace(skill.Category))
                skill.Category = "Soft Skills";
            if (string.IsNullOrWhiteSpace(skill.TypeCompetence))
                skill.TypeCompetence = "Comportemental";
            merged.Add(skill);
        }

        return merged;
    }

    private static bool LooksLikeActivity(CvExperience exp)
    {
        var text = NormalizeKey($"{exp.Role} {exp.Company} {string.Join(" ", exp.Bullets ?? new List<string>())}");
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (NormalizeKey(exp.Role).Contains("stage") || NormalizeKey(exp.Role).Contains("intern"))
            return false;
        return ActivitySignals.Any(signal => text.Contains(signal));
    }

    private static bool IsSameMeaning(string? left, string? right)
    {
        var a = NormalizeKey(left);
        var b = NormalizeKey(right);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        return a == b || a.Contains(b) || b.Contains(a);
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string? NullIfEmpty(string? value)
    {
        var clean = CleanText(value);
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(ch));
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9]+", " ").Trim();
    }

    private async Task<byte[]> RenderPdfWithFallbackAsync(string templateSlug, CvData data, string htmlSnapshot)
    {
        try
        {
            return await _pdfRenderer.RenderPdfAsync(htmlSnapshot);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Chromium-compatible browser executable", StringComparison.OrdinalIgnoreCase))
        {
            var document = CvDocumentFactory.Create(templateSlug, data);
            QuestPDF.Settings.License = LicenseType.Community;
            return document.GeneratePdf();
        }
    }
}

public class CvEngineResult
{
    public CvData CvJson { get; set; } = new();
}
