using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.CvDocuments.Domain;
using NextStep.Modules.CvDocuments.Application.Services;
using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.Api;
using NextStep.Shared.Pagination;
using NextStep.Shared.ErrorHandling;
using NextStep.Modules.CvDocuments.Application.Dtos;

namespace NextStep.Modules.CvDocuments.Api;

[ApiController]
[Route("api/cv")]
[Authorize]
public class CvController : ControllerBase
{
    private readonly ICvService _cvService;
    private readonly ICvTemplateService _templateService;
    private readonly IProfileApi _profile;
    private readonly ITemplateThumbnailService _thumbnailService;

    public CvController(
        ICvService cvService,
        ICvTemplateService templateService,
        IProfileApi profile,
        ITemplateThumbnailService thumbnailService)
    {
        _cvService = cvService;
        _templateService = templateService;
        _profile = profile;
        _thumbnailService = thumbnailService;
    }

    [HttpGet("templates")]
    [AllowAnonymous]
    public async Task<ActionResult<List<CvTemplateDto>>> GetTemplates([FromQuery] CvTemplateFilterQuery filter)
    {
        return Ok(await _templateService.GetTemplatesAsync(filter));
    }

    [HttpGet("templates/filters")]
    [AllowAnonymous]
    public ActionResult<CvTemplateFilterOptions> GetFilterOptions()
    {
        return Ok(_templateService.GetFilterOptions());
    }

    [HttpPost("templates/generate-thumbnails")]
    public async Task<ActionResult<MessageResponse>> GenerateThumbnails()
    {
        await _thumbnailService.GenerateAllThumbnailsAsync();
        return Ok(new MessageResponse("Thumbnails generated for all templates."));
    }

    // Public (loaded by <img> tags, which send no token). A missing thumbnail of a real
    // template is generated lazily, rate-limited by the thumbnail service.
    [HttpGet("templates/{slug}/thumbnail")]
    [AllowAnonymous]
    public async Task<IActionResult> GetThumbnailPng(string slug)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var imageBytes = _thumbnailService.GetThumbnailPng(normalizedSlug);
        if (imageBytes is null && await _thumbnailService.TryGenerateMissingAsync(normalizedSlug))
            imageBytes = _thumbnailService.GetThumbnailPng(normalizedSlug);
        if (imageBytes is null)
            throw new NotFoundException(ThumbnailNotFound(slug));

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return File(imageBytes, "image/png");
    }

    [HttpGet("templates/{slug}/thumbnail.pdf")]
    [AllowAnonymous]
    public IActionResult GetThumbnailPdf(string slug)
    {
        var pdfBytes = _thumbnailService.GetThumbnailPdf(slug.ToLowerInvariant());
        if (pdfBytes is null)
            throw new NotFoundException(ThumbnailNotFound(slug));

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return File(pdfBytes, "application/pdf");
    }

    [HttpPost("preview")]
    public async Task<ActionResult<CvPreviewResult>> Preview([FromQuery] string template = "modern", [FromQuery] Guid? offerId = null)
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        return Ok(await _cvService.PreviewCvAsync(userId, template, offerId));
    }

    [HttpPost("preview/render")]
    public async Task<ActionResult<CvRenderResponse>> PreviewRender([FromBody] CvRenderRequest request)
    {
        return Ok(await _cvService.PreviewFromDataAsync(request));
    }

    [HttpPost("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromBody] CvExportPdfRequest request)
    {
        var pdfBytes = await _cvService.ExportPdfAsync(request);
        return File(pdfBytes, "application/pdf", $"preview-{request.TemplateSlug}.pdf");
    }

    [HttpPost("save")]
    public async Task<ActionResult<CvSaveResult>> Save([FromBody] CvSaveRequest request)
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        var result = await _cvService.SaveCvAsync(userId, request);
        result.FileUrl = BuildCvDownloadFileUrl(result.HistoryId);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CvSaveResult>> Update(Guid id, [FromBody] CvSaveRequest request)
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        var result = await _cvService.UpdateCvAsync(userId, id, request);
        result.FileUrl = BuildCvDownloadFileUrl(result.HistoryId);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<CvHistoryDto>>> GetHistory()
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        var history = await _cvService.GetHistoryAsync(userId);
        foreach (var item in history)
        {
            item.FileUrl = BuildCvDownloadFileUrl(item.Id);
        }
        return Ok(history);
    }

    [HttpGet("history/paged")]
    public async Task<ActionResult<PagedResponse<CvHistoryDto>>> GetHistoryPaged([FromQuery] int offset = 0, [FromQuery] int limit = 10)
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        var page = await _cvService.GetHistoryPagedAsync(userId, offset, limit);
        foreach (var item in page.Items)
        {
            item.FileUrl = BuildCvDownloadFileUrl(item.Id);
        }
        return Ok(page);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CvLoadResult>> Load(Guid id)
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        var result = await _cvService.LoadCvAsync(userId, id);
        result.FileUrl = BuildCvDownloadFileUrl(result.HistoryId);
        return Ok(result);
    }

    [HttpGet("{id}/download")]
    public async Task<ActionResult<CvDownloadResponse>> Download(Guid id)
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        await _cvService.GetDownloadUrlAsync(userId, id);
        return Ok(new CvDownloadResponse(BuildCvDownloadFileUrl(id)));
    }

    [HttpGet("{id}/download-file")]
    public async Task<IActionResult> DownloadFile(Guid id)
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        var bytes = await _cvService.GetDownloadBytesAsync(userId, id);
        return File(bytes, "application/pdf", $"cv-{id}.pdf");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = await _profile.EnsureUserIdAsync(User);
        await _cvService.DeleteCvAsync(userId, id);
        return NoContent();
    }

    private static string ThumbnailNotFound(string slug) =>
        $"Thumbnail not found for '{slug}'. Call POST /api/cv/templates/generate-thumbnails first.";

    private string BuildCvDownloadFileUrl(Guid id)
    {
        return $"{Request.Scheme}://{Request.Host}/api/cv/{id}/download-file";
    }
}
