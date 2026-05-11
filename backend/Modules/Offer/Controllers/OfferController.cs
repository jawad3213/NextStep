using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Identity.Services;
using NextStep.Modules.Offer.DTOs;
using NextStep.Modules.Offer.Services;
using System.Security.Claims;

namespace NextStep.Modules.Offer.Controllers;

[ApiController]
[Route("api/offers")]
[Authorize]
[Produces("application/json")]
public class OfferController(
    IOfferService offerService,
    IPipelineRunnerService pipelineRunner,
    IPdfGenerationService pdfGenService,
    IServiceScopeFactory scopeFactory,
    IUserService userService,
    ILogger<OfferController> logger) : ControllerBase
{
    private async Task<Guid> GetUserIdAsync()
    {
        var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(keycloakId))
        {
            var user = await userService.EnsureUserCreatedAsync(User);
            return user.Id;
        }

        if (Request.Headers.TryGetValue("X-User-Id", out var uid) && Guid.TryParse(uid, out var parsedGuid))
        {
            return parsedGuid;
        }

        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    [HttpPost("submit")]
    [ProducesResponseType(typeof(OfferSubmitResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] OfferSubmitDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var dbUserId = await GetUserIdAsync();
        var rawText = dto.RawText;
        var templateId = dto.TemplateId;
        var userIdStr = dbUserId.ToString();

        logger.LogInformation("POST /api/offers/submit — user={UserId}", dbUserId);

        var offre = await offerService.SaveOfferAsync(rawText, userIdStr);
        var offerId = offre.Id;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IPipelineRunnerService>();
                await runner.RunPipelineAsync(rawText, userIdStr, templateId, offerId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pipeline background task failed for offer {OfferId}", offerId);
            }
        });

        return Accepted(new OfferSubmitResponseDto { OfferId = offerId, Status = "processing" });
    }

    [HttpPost("{id:guid}/generate-pdf")]
    [ProducesResponseType(typeof(PdfGenerateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GeneratePdf(
        Guid id,
        [FromBody] PdfGenerateDto dto,
        CancellationToken ct)
    {
        var dbUserId = await GetUserIdAsync();
        logger.LogInformation("POST /api/offers/{OfferId}/generate-pdf — template={Template}", id, dto.TemplateId);

        try
        {
            var result = await pdfGenService.GeneratePdfAsync(dbUserId, id, dto.TemplateId, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PDF generation failed for offer {OfferId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/analysis")]
    [ProducesResponseType(typeof(OfferAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysis(Guid id, CancellationToken ct)
    {
        var result = await offerService.GetAnalysisAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
