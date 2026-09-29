// ============================================================
// Modules/CvDocuments/Controllers/OfferCvController.cs
// CV endpoints of an offer (moved from OfferController, same routes):
//   GET/PATCH /api/offers/{id}/cv-draft · POST /api/offers/{id}/generate-pdf
// ============================================================
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.CvDocuments.Application.Dtos;
using NextStep.Modules.CvDocuments.Application.Services;
using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.CvDocuments.Api;

[ApiController]
[Route("api/offers")]
[Authorize]
[Produces("application/json")]
public class OfferCvController(
    ICvDraftService cvDraftService,
    IPdfGenerationService pdfGenService,
    IProfileApi profile,
    ILogger<OfferCvController> logger) : ControllerBase
{
    [HttpGet("{id:guid}/cv-draft")]
    [ProducesResponseType(typeof(CvDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CvDraftDto>> GetCvDraft(Guid id, CancellationToken ct)
    {
        var userId = await profile.EnsureUserIdAsync(User);
        return Ok(await cvDraftService.GetCvDraftAsync(userId, id, ct)
                  ?? throw new NotFoundException("CV draft not found."));
    }

    [HttpPatch("{id:guid}/cv-draft")]
    [ProducesResponseType(typeof(CvDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CvDraftDto>> SaveCvDraft(Guid id, [FromBody] JsonElement draft, CancellationToken ct)
    {
        var userId = await profile.EnsureUserIdAsync(User);
        return Ok(await cvDraftService.SaveCvDraftAsync(userId, id, draft, ct));
    }

    [HttpPost("{id:guid}/generate-pdf")]
    [ProducesResponseType(typeof(PdfGenerateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PdfGenerateResultDto>> GeneratePdf(Guid id, [FromBody] PdfGenerateDto dto, CancellationToken ct)
    {
        var userId = await profile.EnsureUserIdAsync(User);
        logger.LogInformation("POST /api/offers/{OfferId}/generate-pdf - template={Template}", id, dto.TemplateId);
        return Ok(await pdfGenService.GeneratePdfAsync(userId, id, dto.TemplateId, ct));
    }
}
