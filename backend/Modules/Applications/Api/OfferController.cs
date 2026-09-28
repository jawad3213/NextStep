// ============================================================
// Modules/Applications/Controllers/OfferController.cs
// Endpoints: POST /api/offers/submit · GET /api/offers/{id}/analysis · GET /api/offers
//            POST /api/offers/skill-gap
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Applications.Api;

[ApiController]
[Route("api/offers")]
[Authorize]
[Produces("application/json")]
public class OfferController(
    IOfferService offerService,
    IOfferAnalysisService analysisService,
    ISkillGapService skillGapService,
    IProfileApi profile,
    ILogger<OfferController> logger) : ControllerBase
{
    private Task<Guid> GetUserIdAsync() => profile.EnsureUserIdAsync(User);

    [HttpPost("submit")]
    [ProducesResponseType(typeof(OfferSubmitResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OfferSubmitResponseDto>> Submit([FromBody] OfferSubmitDto dto)
    {
        // Validation automatique via [ApiController] + ConfigureApiBehavior (400 { error, details }).
        var userId = await GetUserIdAsync();
        logger.LogInformation("POST /api/offers/submit - user={UserId}", userId);

        // Step 1 only stores the offer. The three-agent analysis is launched
        // explicitly from step 2 through the analyze-sync endpoint.
        var offer = await offerService.SaveOfferAsync(dto.RawText, userId.ToString());
        return Accepted(new OfferSubmitResponseDto { OfferId = offer.Id, Status = "saved" });
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OfferHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OfferHistoryItemDto>>> GetHistory(CancellationToken ct) =>
        Ok(await offerService.GetHistoryAsync(await GetUserIdAsync(), ct));

    [HttpDelete]
    public Task<ActionResult<DeleteOffersResponseDto>> BulkDelete([FromBody] BulkDeleteOffersDto dto, CancellationToken ct) =>
        DeleteOffersAsync(dto, ct);

    [HttpPost("bulk-delete")]
    public Task<ActionResult<DeleteOffersResponseDto>> BulkDeletePost([FromBody] BulkDeleteOffersDto dto, CancellationToken ct) =>
        DeleteOffersAsync(dto, ct);

    [HttpPost("delete")]
    public Task<ActionResult<DeleteOffersResponseDto>> DeletePost([FromBody] BulkDeleteOffersDto dto, CancellationToken ct) =>
        DeleteOffersAsync(dto, ct);

    [HttpPost("{id:guid}/analyze-sync")]
    [ProducesResponseType(typeof(OfferAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OfferAnalysisDto>> AnalyzeSync(Guid id, [FromBody] ResumePipelineDto dto, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        logger.LogInformation("POST /api/offers/{OfferId}/analyze-sync - user={UserId}", id, userId);
        return Ok(await analysisService.AnalyzeAsync(userId, id, ct));
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<OfferSubmitResponseDto>> Resume(Guid id, [FromBody] ResumePipelineDto dto)
    {
        var userId = await GetUserIdAsync();
        logger.LogInformation("POST /api/offers/{OfferId}/resume - template={Template}", id, dto.TemplateId);

        analysisService.StartGenerationInBackground(userId, id, dto.TemplateId);
        return Accepted(new OfferSubmitResponseDto { OfferId = id, Status = "generating" });
    }

    /// <summary>Skill gap between a pasted offer and the user's profile. Nothing is saved.</summary>
    [HttpPost("skill-gap")]
    [ProducesResponseType(typeof(SkillGapResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SkillGapResultDto>> SkillGap([FromBody] SkillGapRequestDto dto, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        logger.LogInformation("POST /api/offers/skill-gap - user={UserId}", userId);
        return Ok(await skillGapService.AnalyzeAsync(userId, dto.OfferText, ct));
    }

    [HttpGet("{id:guid}/analysis")]
    [ProducesResponseType(typeof(OfferAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfferAnalysisDto>> GetAnalysis(Guid id, CancellationToken ct) =>
        Ok(await offerService.GetAnalysisAsync(await GetUserIdAsync(), id, ct)
           ?? throw new NotFoundException("Analyse introuvable."));

    private async Task<ActionResult<DeleteOffersResponseDto>> DeleteOffersAsync(BulkDeleteOffersDto dto, CancellationToken ct)
    {
        var deleted = await offerService.DeleteOffersAsync(await GetUserIdAsync(), dto.OfferIds, ct);
        return Ok(new DeleteOffersResponseDto(deleted));
    }
}
