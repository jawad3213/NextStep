using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Cv.Models;
using NextStep.Modules.Cv.Services;
using NextStep.Modules.Identity.Services;

namespace NextStep.Modules.Cv.Controllers;

[ApiController]
[Route("api/cv")]
[Authorize]
public class CvController : ControllerBase
{
    private readonly ICvService _cvService;
    private readonly ICvTemplateService _templateService;
    private readonly IUserService _userService;
    private readonly ITemplateThumbnailService _thumbnailService;

    public CvController(ICvService cvService, ICvTemplateService templateService, IUserService userService, ITemplateThumbnailService thumbnailService)
    {
        _cvService        = cvService;
        _templateService  = templateService;
        _userService      = userService;
        _thumbnailService = thumbnailService;
    }

    // ─── Step 1: Template selection ────────────────────────────

    /// <summary>
    /// List all available CV templates with optional filtering.
    /// GET /api/cv/templates?industry=ITAndEngineering&style=Modern
    /// </summary>
    [HttpGet("templates")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTemplates([FromQuery] CvTemplateFilterQuery filter)
    {
        var templates = await _templateService.GetTemplatesAsync(filter);
        return Ok(templates);
    }

    /// <summary>
    /// Get all available filter options for the template gallery dropdowns.
    /// GET /api/cv/templates/filters
    /// </summary>
    [HttpGet("templates/filters")]
    [AllowAnonymous]
    public IActionResult GetFilterOptions()
    {
        return Ok(_templateService.GetFilterOptions());
    }

    // ─── Template Thumbnails (sample-data PDF previews) ──────

    /// <summary>
    /// Generate preview PDFs for all 5 templates using sample data.
    /// POST /api/cv/templates/generate-thumbnails
    /// </summary>
    [HttpPost("templates/generate-thumbnails")]
    [AllowAnonymous]
    public async Task<IActionResult> GenerateThumbnails()
    {
        await _thumbnailService.GenerateAllThumbnailsAsync();
        return Ok(new { message = "Thumbnails generated for all templates." });
    }

    /// <summary>
    /// Get the preview PDF for a template (rendered with sample data).
    /// GET /api/cv/templates/{slug}/thumbnail
    /// </summary>
    [HttpGet("templates/{slug}/thumbnail")]
    [AllowAnonymous]
    public IActionResult GetThumbnail(string slug)
    {
        var pdfBytes = _thumbnailService.GetThumbnailPdf(slug.ToLowerInvariant());
        if (pdfBytes is null)
            return NotFound(new { error = $"Thumbnail not found for '{slug}'. Call POST /api/cv/templates/generate-thumbnails first." });
        return File(pdfBytes, "application/pdf", $"{slug}-preview.pdf");
    }

    // ─── Step 2: Preview (no storage) ──────────────────────────

    /// <summary>
    /// Generate an initial CV preview from the user's profile data.
    /// Returns the PDF bytes + the CvData JSON for the editor.
    /// POST /api/cv/preview?template=modern
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromQuery] string template = "modern", [FromQuery] Guid? offerId = null)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            var result = await _cvService.PreviewCvAsync(user.Id, template, offerId);

            // Return both the PDF and the CvData as a multipart-like JSON response
            return Ok(new
            {
                pdfBase64 = Convert.ToBase64String(result.PdfBytes),
                data      = result.Data,
            });
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Re-render the PDF preview from user-edited CvData (real-time editing).
    /// No storage — just returns fresh PDF bytes.
    /// POST /api/cv/preview/render?template=modern
    /// </summary>
    [HttpPost("preview/render")]
    public IActionResult PreviewRender([FromQuery] string template, [FromBody] CvData data)
    {
        try
        {
            var pdfBytes = _cvService.PreviewFromData(template, data);
            return File(pdfBytes, "application/pdf", $"preview-{template}.pdf");
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    // ─── Step 3: Save ("Sauvegarder") ──────────────────────────

    /// <summary>
    /// Save the final CV — generates PDF, uploads to MinIO, saves history.
    /// Called when the user clicks "Sauvegarder".
    /// POST /api/cv/save
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] CvSaveRequest request)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            var result = await _cvService.SaveCvAsync(user.Id, request);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Update an existing saved CV — re-generates PDF, re-uploads, updates history.
    /// PUT /api/cv/{id}
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CvSaveRequest request)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            var result = await _cvService.UpdateCvAsync(user.Id, id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    // ─── History & Management ──────────────────────────────────

    /// <summary>
    /// Get all saved CVs for the authenticated user (most recent first).
    /// GET /api/cv/history
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var user = await _userService.EnsureUserCreatedAsync(User);
        var history = await _cvService.GetHistoryAsync(user.Id);
        return Ok(history);
    }

    /// <summary>
    /// Load a saved CV for re-editing (returns CvData + template info).
    /// GET /api/cv/{id}
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Load(Guid id)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            var result = await _cvService.LoadCvAsync(user.Id, id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Get a fresh pre-signed download URL for a saved CV.
    /// GET /api/cv/{id}/download
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            var url = await _cvService.GetDownloadUrlAsync(user.Id, id);
            return Ok(new { downloadUrl = url });
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Download CV bytes via backend proxy (avoids browser DNS issues with internal MinIO host).
    /// GET /api/cv/{id}/download-file
    /// </summary>
    [HttpGet("{id}/download-file")]
    public async Task<IActionResult> DownloadFile(Guid id)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            var bytes = await _cvService.GetDownloadBytesAsync(user.Id, id);
            return File(bytes, "application/pdf", $"cv-{id}.pdf");
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Delete a saved CV (removes from MinIO + database).
    /// DELETE /api/cv/{id}
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            await _cvService.DeleteCvAsync(user.Id, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}
