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

    /// <summary>
    /// Starts the three-agent analysis and returns immediately (202 Accepted).
    /// The analysis is persisted on the offer, so the client polls it until it appears;
    /// progress is also pushed over SignalR. The analysis can take minutes, longer than the
    /// idle timeout of any reverse proxy, so keeping the HTTP request open would have the
    /// connection dropped mid-run.
    /// </summary>
    [HttpPost("{id:guid}/analyze-sync")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnalyzeSync(Guid id, [FromBody] ResumePipelineDto dto)
    {
        var userId = await GetUserIdAsync();
        logger.LogInformation("POST /api/offers/{OfferId}/analyze-sync (async) - user={UserId}", id, userId);

        // The run happens in the background, so ownership has to be checked up front:
        // otherwise an unknown or foreign offer would be acknowledged with 202 and the
        // caller would only learn about it through a timeout.
        if (!await offerService.OfferBelongsToUserAsync(userId, id))
        {
            return NotFound(new { error = "Offer not found." });
        }

        analysisService.StartAnalysisInBackground(userId, id);
        return Accepted(new { offerId = id, status = "analysis_started" });
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
    public async Task<ActionResult<OfferAnalysisDto>> GetAnalysis(Guid id, CancellationToken ct)
    {
        // GetAnalysisAsync throws NotFound when the offer is unknown or belongs to somebody
        // else, so reaching this point means the caller owns the offer. A null analysis is
        // then a normal state, not a failure: the agents store their result only when they
        // are done. Answering 200 with no body lets polling clients wait for the result
        // instead of treating "not ready yet" as an error.
        return Ok(await offerService.GetAnalysisAsync(await GetUserIdAsync(), id, ct));
    }

    private async Task<ActionResult<DeleteOffersResponseDto>> DeleteOffersAsync(BulkDeleteOffersDto dto, CancellationToken ct)
    {
        var deleted = await offerService.DeleteOffersAsync(await GetUserIdAsync(), dto.OfferIds, ct);
        return Ok(new DeleteOffersResponseDto(deleted));
    }
}
