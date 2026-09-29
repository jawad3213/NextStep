using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.Modules.CvDocuments.Infrastructure.Persistence;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.CvDocuments.Domain;
using NextStep.Modules.CvDocuments.Templates;
using NextStep.Shared.Pagination;
using NextStep.Shared.Http;
using NextStep.Shared.Storage;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using NextStep.Modules.CvDocuments.Application.Dtos;
using NextStep.Shared.ErrorHandling;
using NextStep.Modules.CvDocuments.Application.Mappings;

namespace NextStep.Modules.CvDocuments.Application.Services;

public interface ICvService
{
    Task<CvPreviewResult> PreviewCvAsync(Guid userId, string templateId, Guid? jobId = null);
    Task<CvRenderResponse> PreviewFromDataAsync(CvRenderRequest request);
    Task<byte[]> ExportPdfAsync(CvExportPdfRequest request);
    Task<CvSaveResult> SaveCvAsync(Guid userId, CvSaveRequest request);
    Task<CvSaveResult> UpdateCvAsync(Guid userId, Guid historyId, CvSaveRequest request);
    Task<List<CvHistoryDto>> GetHistoryAsync(Guid userId);
    Task<PagedResponse<CvHistoryDto>> GetHistoryPagedAsync(Guid userId, int offset, int limit);
    Task<CvLoadResult> LoadCvAsync(Guid userId, Guid historyId);
    Task<string> GetDownloadUrlAsync(Guid userId, Guid historyId);
    Task<byte[]> GetDownloadBytesAsync(Guid userId, Guid historyId);
    Task DeleteCvAsync(Guid userId, Guid historyId);
}

public class CvService : ICvService
{
    private static readonly TimeSpan PdfRenderTimeout = TimeSpan.FromSeconds(15);

    private readonly IApplicationsApi _applications;
    private readonly IStorageService _storageService;
    private readonly MinioOptions _minioOptions;
    private readonly CvDocumentsDbContext _db;
    private readonly IAgentHttpClient _agentClient;
    private readonly ICvHtmlTemplateRenderer _htmlTemplateRenderer;
    private readonly ICvPdfRenderer _pdfRenderer;
    private readonly ILogger<CvService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public CvService(
        IApplicationsApi applications,
        IStorageService storageService,
        IOptions<MinioOptions> minioOptions,
        CvDocumentsDbContext db,
        IAgentHttpClient agentClient,
        ICvHtmlTemplateRenderer htmlTemplateRenderer,
        ICvPdfRenderer pdfRenderer,
        ILogger<CvService> logger)
    {
        _applications = applications;
        _storageService = storageService;
        _minioOptions = minioOptions.Value;
        _db = db;
        _agentClient = agentClient;
        _htmlTemplateRenderer = htmlTemplateRenderer;
        _pdfRenderer = pdfRenderer;
        _logger = logger;
    }

    public async Task<CvPreviewResult> PreviewCvAsync(Guid userId, string templateId, Guid? jobId = null)
    {
        object? offerData = null;
        if (jobId.HasValue)
        {
            var offer = await _applications.GetOfferSummaryAsync(userId, jobId.Value);
            if (offer != null)
            {
                offerData = new
                {
                    titre = offer.Title,
                    description = offer.Description,
                    competences_requises = offer.RequiredSkills,
                    keywords_ats = offer.AtsKeywords
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

        var data = CvDataSanitizer.Sanitize(response.CvJson ?? new CvData());
        data.ThemeColor = null;

        if (jobId.HasValue && jobId.Value != Guid.Empty)
        {
            // Stored on the application only if one already exists (no creation on preview).
            await _applications.SaveCvDocumentAsync(userId, jobId.Value,
                JsonSerializer.Serialize(data, _jsonOptions), pdfUrl: null, createApplicationIfMissing: false);
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
        request.Data = CvDataSanitizer.Sanitize(request.Data);
        return await _htmlTemplateRenderer.RenderAsync(request.TemplateSlug, request.Data, request.DesignConfig);
    }

    public async Task<byte[]> ExportPdfAsync(CvExportPdfRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Data = CvDataSanitizer.Sanitize(request.Data);

        var html = string.IsNullOrWhiteSpace(request.HtmlSnapshot)
            ? (await _htmlTemplateRenderer.RenderAsync(request.TemplateSlug, request.Data, request.DesignConfig)).Html
            : request.HtmlSnapshot!;

        return await RenderPdfWithFallbackAsync(request.TemplateSlug, request.Data, html);
    }

    public async Task<CvSaveResult> SaveCvAsync(Guid userId, CvSaveRequest request)
    {
        request.Data = CvDataSanitizer.Sanitize(request.Data);
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
            await _applications.SaveCvDocumentAsync(userId, request.OfferId.Value,
                JsonSerializer.Serialize(request.Data, _jsonOptions), pdfUrl: fileUrl, createApplicationIfMissing: true);
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
        request.Data = CvDataSanitizer.Sanitize(request.Data);
        var renderResult = await _htmlTemplateRenderer.RenderAsync(request.TemplateSlug, request.Data, request.DesignConfig);
        var htmlSnapshot = request.HtmlSnapshot ?? renderResult.Html;

        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new NotFoundException("CV not found.");

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
        var histories = await _db.CvHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        bool hasUpdates = false;
        foreach (var h in histories)
        {
            if (h.Title != null && h.Title.StartsWith("CV_") && h.Title.Length > 30)
            {
                var offerIdString = h.Title.Substring(3);
                if (Guid.TryParse(offerIdString, out var offerId))
                {
                    var offer = await _applications.GetOfferSummaryAsync(userId, offerId);
                    if (offer != null)
                    {
                        h.Title = $"CV - {offer.Title} - {offer.Company}";
                        hasUpdates = true;
                    }
                }
            }
        }

        if (hasUpdates)
        {
            await _db.SaveChangesAsync();
        }

        return histories.Select(h => h.ToDto()).ToList();
    }

    public async Task<PagedResponse<CvHistoryDto>> GetHistoryPagedAsync(Guid userId, int offset, int limit)
    {
        var safeOffset = Math.Max(0, offset);
        var safeLimit = Math.Clamp(limit, 1, 100);

        var baseQuery = _db.CvHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt);

        var total = await baseQuery.CountAsync();
        var histories = await baseQuery
            .Skip(safeOffset)
            .Take(safeLimit)
            .ToListAsync();

        bool hasUpdates = false;
        foreach (var h in histories)
        {
            if (h.Title != null && h.Title.StartsWith("CV_") && h.Title.Length > 30)
            {
                var offerIdString = h.Title.Substring(3);
                if (Guid.TryParse(offerIdString, out var offerId))
                {
                    var offer = await _applications.GetOfferSummaryAsync(userId, offerId);
                    if (offer != null)
                    {
                        h.Title = $"CV - {offer.Title} - {offer.Company}";
                        hasUpdates = true;
                    }
                }
            }
        }

        if (hasUpdates)
        {
            await _db.SaveChangesAsync();
        }

        var items = histories.Select(h => h.ToDto()).ToList();

        return new PagedResponse<CvHistoryDto>
        {
            Offset = safeOffset,
            Limit = safeLimit,
            Total = total,
            HasMore = safeOffset + items.Count < total,
            Items = items
        };
    }

    public async Task<CvLoadResult> LoadCvAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new NotFoundException("CV not found.");

        var data = CvDataSanitizer.Sanitize(JsonSerializer.Deserialize<CvData>(history.CvDataJson, _jsonOptions) ?? new CvData());
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
            ?? throw new NotFoundException("CV not found.");

        return await _storageService.GetPresignedUrlAsync(history.ObjectKey, TimeSpan.FromHours(1));
    }

    public async Task<byte[]> GetDownloadBytesAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new NotFoundException("CV not found.");

        return await _storageService.DownloadFileAsync(history.ObjectKey);
    }

    public async Task DeleteCvAsync(Guid userId, Guid historyId)
    {
        var history = await _db.CvHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.UserId == userId)
            ?? throw new NotFoundException("CV not found.");

        _db.CvHistories.Remove(history);
        await _db.SaveChangesAsync();
    }

    private async Task<byte[]> RenderPdfWithFallbackAsync(string templateSlug, CvData data, string htmlSnapshot)
    {
        try
        {
            return await _pdfRenderer.RenderPdfAsync(htmlSnapshot).WaitAsync(PdfRenderTimeout);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Puppeteer PDF render timed out for template {Template}. Falling back to QuestPDF.", templateSlug);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Chromium-compatible browser executable", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "No Chromium-compatible browser available for template {Template}. Falling back to QuestPDF.", templateSlug);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Puppeteer PDF render failed for template {Template}. Falling back to QuestPDF.", templateSlug);
        }

        var document = CvDocumentFactory.Create(templateSlug, data);
        QuestPDF.Settings.License = LicenseType.Community;
        return document.GeneratePdf();
    }

    /// <summary>Response of the agents' /prepare-cv endpoint.</summary>
    private sealed class CvEngineResult
    {
        public CvData CvJson { get; set; } = new();
    }
}
