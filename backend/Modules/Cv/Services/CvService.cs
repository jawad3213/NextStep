using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using NextStep.data;
using NextStep.Modules.Cv.Models;
using NextStep.Modules.Cv.Templates;
using NextStep.Modules.Profile.Services;
using NextStep.Shared.Storage;
using NextStep.Shared.Http;

namespace NextStep.Modules.Cv.Services;

public interface ICvService
{
    /// <summary>
    /// Preview only — generates PDF from the user's profile data. No storage.
    /// Used when the user first picks a template.
    /// </summary>
    Task<CvPreviewResult> PreviewCvAsync(Guid userId, string templateId, Guid? jobId = null);

    /// <summary>
    /// Preview from edited data — generates PDF from the CvData the user modified.
    /// Used during real-time editing to refresh the preview.
    /// </summary>
    byte[] PreviewFromData(string templateId, CvData data);

    /// <summary>
    /// Save — generates the final PDF from edited data, uploads to MinIO,
    /// and saves a history record. Called when the user clicks "Sauvegarder".
    /// </summary>
    Task<CvSaveResult> SaveCvAsync(Guid userId, CvSaveRequest request);

    /// <summary>
    /// Update an existing saved CV — re-generates the PDF, re-uploads, updates history.
    /// </summary>
    Task<CvSaveResult> UpdateCvAsync(Guid userId, Guid historyId, CvSaveRequest request);

    /// <summary>
    /// Get all saved CVs for a user (most recent first).
    /// </summary>
    Task<List<CvHistoryDto>> GetHistoryAsync(Guid userId);

    /// <summary>
    /// Load a saved CV for re-editing (returns the CvData + template info).
    /// </summary>
    Task<CvLoadResult> LoadCvAsync(Guid userId, Guid historyId);

    /// <summary>
    /// Get a fresh pre-signed download URL for a saved CV.
    /// </summary>
    Task<string> GetDownloadUrlAsync(Guid userId, Guid historyId);

    /// <summary>
    /// Delete a saved CV (removes from MinIO + database).
    /// </summary>
    Task DeleteCvAsync(Guid userId, Guid historyId);
}

// ─── DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Returned when the user first picks a template — PDF preview + the initial CvData.
/// The frontend uses CvData to populate the editor.
/// </summary>
public class CvPreviewResult
{
    public byte[] PdfBytes { get; set; } = Array.Empty<byte>();
    public CvData Data { get; set; } = new();
}

/// <summary>
/// Request body when the user clicks "Sauvegarder".
/// </summary>
public class CvSaveRequest
{
    public string TemplateSlug { get; set; } = string.Empty;
    public string? Title { get; set; }
    public CvData Data { get; set; } = new();
}

/// <summary>
/// Returned after a successful save.
/// </summary>
public class CvSaveResult
{
    public Guid HistoryId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

/// <summary>
/// Returned when loading a saved CV for re-editing.
/// </summary>
public class CvLoadResult
{
    public Guid HistoryId { get; set; }
    public string TemplateSlug { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string? Title { get; set; }
    public CvData Data { get; set; } = new();
    public string FileUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for the CV history listing.
/// </summary>
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

// ─── Implementation ────────────────────────────────────────────

public class CvService : ICvService
{
    private readonly IProfileService _profileService;
    private readonly IOfferService _offerService;
    private readonly IStorageService _storageService;
    private readonly MinioOptions _minioOptions;
    private readonly AppDbContext _db;
    private readonly IAgentHttpClient _agentClient;

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
        IAgentHttpClient agentClient)
    {
        _profileService = profileService;
        _offerService   = offerService;
        _storageService = storageService;
        _minioOptions   = minioOptions.Value;
        _db             = db;
        _agentClient    = agentClient;
    }

    // ─── Preview (no storage) ──────────────────────────────────

    public async Task<CvPreviewResult> PreviewCvAsync(Guid userId, string templateId, Guid? jobId = null)
    {
        // 1. Préparer le contexte de l'offre si présent
        object? offerData = null;
        if (jobId.HasValue)
        {
            var analysis = await _offerService.GetAnalysisAsync(jobId.Value);
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

        // 2. Appeler l'API Python pour obtenir le CvData prêt et scoré
        var request = new 
        { 
            user_id = userId.ToString(), 
            template_slug = templateId,
            offer_data = offerData
        };
        
        var response = await _agentClient.PostAsync<object, CvEngineResult>("/prepare-cv", request);
        
        var data = response.CvJson ?? new CvData();

        // 3. Render PDF avec QuestPDF
        var document = CvDocumentFactory.Create(templateId, data);
        QuestPDF.Settings.License = LicenseType.Community;
        var pdfBytes = document.GeneratePdf();

        return new CvPreviewResult
        {
            PdfBytes = pdfBytes,
            Data     = data,
        };
    }

    public byte[] PreviewFromData(string templateId, CvData data)
    {
        var document = CvDocumentFactory.Create(templateId, data);
        QuestPDF.Settings.License = LicenseType.Community;
        return document.GeneratePdf();
    }

    // ─── Save (MinIO + DB) ─────────────────────────────────────

    public async Task<CvSaveResult> SaveCvAsync(Guid userId, CvSaveRequest request)
    {
        // 1. Generate final PDF from the user-edited data
        var pdfBytes = PreviewFromData(request.TemplateSlug, request.Data);

        // 2. Upload to MinIO
        var objectKey = $"cvs/{userId}/{Guid.NewGuid()}.pdf";
        var fileUrl = await _storageService.UploadFileAsync(objectKey, pdfBytes, "application/pdf");

        // 3. Save history record with the CvData JSON snapshot
        var history = new CvHistory
        {
            Id            = Guid.NewGuid(),
            UserId        = userId,
            Title         = request.Title,
            TemplateSlug  = request.TemplateSlug.ToLowerInvariant(),
            TemplateName  = request.TemplateSlug,
            CvDataJson    = JsonSerializer.Serialize(request.Data, _jsonOptions),
            FileUrl       = fileUrl,
            ObjectKey     = objectKey,
            BucketName    = _minioOptions.BucketName,
            FileSizeBytes = pdfBytes.Length,
            CreatedAt     = DateTime.UtcNow,
        };

        _db.CvHistories.Add(history);
        await _db.SaveChangesAsync();

        return new CvSaveResult
        {
            HistoryId     = history.Id,
            FileUrl       = fileUrl,
            FileSizeBytes = pdfBytes.Length,
        };
    }

    public async Task<CvSaveResult> UpdateCvAsync(Guid userId, Guid historyId, CvSaveRequest request)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        // 1. Generate new PDF
        var pdfBytes = PreviewFromData(request.TemplateSlug, request.Data);

        // 2. Upload new version to MinIO (new object key)
        var objectKey = $"cvs/{userId}/{Guid.NewGuid()}.pdf";
        var fileUrl = await _storageService.UploadFileAsync(objectKey, pdfBytes, "application/pdf");

        // 3. Update the history record
        history.Title         = request.Title;
        history.TemplateSlug  = request.TemplateSlug.ToLowerInvariant();
        history.TemplateName  = request.TemplateSlug;
        history.CvDataJson    = JsonSerializer.Serialize(request.Data, _jsonOptions);
        history.FileUrl       = fileUrl;
        history.ObjectKey     = objectKey;
        history.FileSizeBytes = pdfBytes.Length;
        history.UpdatedAt     = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new CvSaveResult
        {
            HistoryId     = history.Id,
            FileUrl       = fileUrl,
            FileSizeBytes = pdfBytes.Length,
        };
    }

    // ─── History & Load ────────────────────────────────────────

    public async Task<List<CvHistoryDto>> GetHistoryAsync(Guid userId)
    {
        return await _db.CvHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new CvHistoryDto
            {
                Id            = h.Id,
                Title         = h.Title,
                TemplateSlug  = h.TemplateSlug,
                TemplateName  = h.TemplateName,
                FileUrl       = h.FileUrl,
                FileSizeBytes = h.FileSizeBytes,
                CreatedAt     = h.CreatedAt,
                UpdatedAt     = h.UpdatedAt,
            })
            .ToListAsync();
    }

    public async Task<CvLoadResult> LoadCvAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        var data = JsonSerializer.Deserialize<CvData>(history.CvDataJson, _jsonOptions)
                   ?? new CvData();

        return new CvLoadResult
        {
            HistoryId    = history.Id,
            TemplateSlug = history.TemplateSlug,
            TemplateName = history.TemplateName,
            Title        = history.Title,
            Data         = data,
            FileUrl      = history.FileUrl,
            CreatedAt    = history.CreatedAt,
            UpdatedAt    = history.UpdatedAt,
        };
    }

    public async Task<string> GetDownloadUrlAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        return await _storageService.GetPresignedUrlAsync(history.ObjectKey, TimeSpan.FromHours(1));
    }

    public async Task DeleteCvAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new KeyNotFoundException("CV not found.");

        _db.CvHistories.Remove(history);
        await _db.SaveChangesAsync();
    }

}

// Helper class for Python response deserialization
public class CvEngineResult
{
    public CvData CvJson { get; set; } = new();
    // We can also map ats_coverage if needed
}
