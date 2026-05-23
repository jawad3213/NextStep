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

    public CvController(
        ICvService cvService,
        ICvTemplateService templateService,
        IUserService userService,
        ITemplateThumbnailService thumbnailService)
    {
        _cvService = cvService;
        _templateService = templateService;
        _userService = userService;
        _thumbnailService = thumbnailService;
    }

    [HttpGet("templates")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTemplates([FromQuery] CvTemplateFilterQuery filter)
    {
        return Ok(await _templateService.GetTemplatesAsync(filter));
    }

    [HttpGet("templates/filters")]
    [AllowAnonymous]
    public IActionResult GetFilterOptions()
    {
        return Ok(_templateService.GetFilterOptions());
    }

    [HttpPost("templates/generate-thumbnails")]
    [AllowAnonymous]
    public async Task<IActionResult> GenerateThumbnails()
    {
        await _thumbnailService.GenerateAllThumbnailsAsync();
        return Ok(new { message = "Thumbnails generated for all templates." });
    }

    [HttpGet("templates/{slug}/thumbnail")]
    [AllowAnonymous]
    public IActionResult GetThumbnailPng(string slug)
    {
        var imageBytes = _thumbnailService.GetThumbnailPng(slug.ToLowerInvariant());
        if (imageBytes is null)
            return NotFound(new { error = $"Thumbnail not found for '{slug}'. Call POST /api/cv/templates/generate-thumbnails first." });

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
            return NotFound(new { error = $"Thumbnail not found for '{slug}'. Call POST /api/cv/templates/generate-thumbnails first." });

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return File(pdfBytes, "application/pdf");
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromQuery] string template = "modern", [FromQuery] Guid? offerId = null)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            return Ok(await _cvService.PreviewCvAsync(user.Id, template, offerId));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("preview/render")]
    [AllowAnonymous]
    public async Task<IActionResult> PreviewRender([FromBody] CvRenderRequest request)
    {
        try
        {
            return Ok(await _cvService.PreviewFromDataAsync(request));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("export/pdf")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportPdf([FromBody] CvExportPdfRequest request)
    {
        try
        {
            var pdfBytes = await _cvService.ExportPdfAsync(request);
            return File(pdfBytes, "application/pdf", $"preview-{request.TemplateSlug}.pdf");
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] CvSaveRequest request)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            return Ok(await _cvService.SaveCvAsync(user.Id, request));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CvSaveRequest request)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            return Ok(await _cvService.UpdateCvAsync(user.Id, id, request));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var user = await _userService.EnsureUserCreatedAsync(User);
        return Ok(await _cvService.GetHistoryAsync(user.Id));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Load(Guid id)
    {
        try
        {
            var user = await _userService.EnsureUserCreatedAsync(User);
            return Ok(await _cvService.LoadCvAsync(user.Id, id));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

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
