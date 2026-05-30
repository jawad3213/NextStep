// ============================================================
// Modules/Offer/Controllers/OfferController.cs
// Endpoints: POST /api/offers/submit · GET /api/offers/{id}/analysis · GET /api/offers
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Identity.Services;
using NextStep.Modules.Offer.DTOs;
using NextStep.Modules.Offer.Services;
using NextStep.Shared.Http;
using System.Security.Claims;
using System.Text.Json;

namespace NextStep.Modules.Offer.Controllers;

[ApiController]
[Route("api/offers")]
[Authorize]
[Produces("application/json")]
public class OfferController(
    IOfferService offerService,
    IPdfGenerationService pdfGenService,
    IServiceScopeFactory scopeFactory,
    IUserService userService,
    ILogger<OfferController> logger) : ControllerBase
{
    private async Task<Guid> GetUserIdAsync()
    {
        var user = await userService.EnsureUserCreatedAsync(User);
        return user.Id;
    }

    [HttpPost("submit")]
    [ProducesResponseType(typeof(OfferSubmitResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] OfferSubmitDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            logger.LogWarning(
                "POST /api/offers/submit - validation failed: {Errors}",
                JsonSerializer.Serialize(errors));

            return BadRequest(new
            {
                error = "Validation failed",
                details = errors
            });
        }

        var dbUserId = await GetUserIdAsync();
        var userIdStr = dbUserId.ToString();

        logger.LogInformation("POST /api/offers/submit - user={UserId}", dbUserId);

        // Step 1 only stores the offer. The three-agent analysis is launched
        // explicitly from step 2 through the analyze-sync endpoint.
        var offre = await offerService.SaveOfferAsync(dto.RawText, userIdStr);
        var offerId = offre.Id;

        return Accepted(new OfferSubmitResponseDto { OfferId = offerId, Status = "saved" });
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OfferHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
    {
        var dbUserId = await GetUserIdAsync();
        var items = await offerService.GetHistoryAsync(dbUserId, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}/cv-draft")]
    [ProducesResponseType(typeof(CvDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCvDraft(Guid id, CancellationToken ct)
    {
        var dbUserId = await GetUserIdAsync();

        try
        {
            var draft = await offerService.GetCvDraftAsync(dbUserId, id, ct);
            return draft is null ? NotFound() : Ok(draft);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPatch("{id:guid}/cv-draft")]
    [ProducesResponseType(typeof(CvDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveCvDraft(Guid id, [FromBody] JsonElement draft, CancellationToken ct)
    {
        var dbUserId = await GetUserIdAsync();

        try
        {
            var saved = await offerService.SaveCvDraftAsync(dbUserId, id, draft, ct);
            return Ok(saved);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteOffersDto dto, CancellationToken ct)
    {
        var dbUserId = await GetUserIdAsync();
        var deleted = await offerService.DeleteOffersAsync(dbUserId, dto.OfferIds, ct);
        return Ok(new { deletedCount = deleted });
    }

    [HttpPost("bulk-delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkDeletePost([FromBody] BulkDeleteOffersDto dto, CancellationToken ct)
    {
        var dbUserId = await GetUserIdAsync();
        var deleted = await offerService.DeleteOffersAsync(dbUserId, dto.OfferIds, ct);
        return Ok(new { deletedCount = deleted });
    }

    [HttpPost("delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePost([FromBody] BulkDeleteOffersDto dto, CancellationToken ct)
    {
        var dbUserId = await GetUserIdAsync();
        var deleted = await offerService.DeleteOffersAsync(dbUserId, dto.OfferIds, ct);
        return Ok(new { deletedCount = deleted });
    }

    [HttpPost("{id:guid}/analyze-sync")]
    [ProducesResponseType(typeof(OfferAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzeSync(Guid id, [FromBody] ResumePipelineDto dto, CancellationToken ct)
    {
        var dbUserId = await GetUserIdAsync();
        var userIdStr = dbUserId.ToString();

        logger.LogInformation("POST /api/offers/{OfferId}/analyze-sync - user={UserId}", id, dbUserId);

        try
        {
            // 1. Retrieve the existing offer
            var offre = await offerService.GetOfferWithAnalysisAsync(dbUserId, id, ct);
            if (offre == null)
            {
                return NotFound(new { error = "Offer not found." });
            }

            var rawText = offre.TexteBrut ?? "";
            // 2. Call the 3 python agents (strictly Analyze Offer -> Retrieve Profile -> Match)
            using var scope = scopeFactory.CreateScope();
            var agentClient = scope.ServiceProvider.GetRequiredService<IAgentHttpClient>();

            // a) Agent 1: Offer Analysis
            var analyzeDoc = await agentClient.AnalyzeOfferAsync(rawText, userIdStr, ct);
            var analyzedOffer = analyzeDoc.RootElement.GetProperty("analyzed_offer");

            // b) Agents 2 & 3: Profile Retriever & Skill Gap Match
            var matchDoc = await agentClient.MatchProfileAsync(userIdStr, analyzedOffer, ct);

            // Merge the results to save into DB
            var combinedDict = new Dictionary<string, object>
            {
                { "analyzed_offer", JsonSerializer.Deserialize<object>(analyzedOffer.GetRawText())! },
                { "skill_gap_analysis", JsonSerializer.Deserialize<object>(matchDoc.RootElement.GetRawText())! },
                { "match_result", JsonSerializer.Deserialize<object>(matchDoc.RootElement.GetRawText())! }
            };
            if (matchDoc.RootElement.TryGetProperty("profile_data", out var profileDataEl))
            {
                combinedDict["profile_data"] = JsonSerializer.Deserialize<object>(profileDataEl.GetRawText())!;
            }
            var combinedJson = JsonSerializer.Serialize(combinedDict);
            using var agentResponse = JsonDocument.Parse(combinedJson);

            // 3. Store the response in the DB
            await offerService.SavePipelineResultAsync(id, agentResponse, dbUserId, ct);

            // 4. Give the response to the frontend
            var analysisDto = await offerService.GetAnalysisAsync(dbUserId, id, ct);

            if (analysisDto == null)
            {
                return StatusCode(500, new { error = "Failed to retrieve analysis after saving." });
            }

            return Ok(analysisDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Synchronous analysis failed for offer {OfferId} / user {UserId}", id, dbUserId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, [FromBody] ResumePipelineDto dto)
    {
        var dbUserId = await GetUserIdAsync();
        var userIdStr = dbUserId.ToString();

        logger.LogInformation("POST /api/offers/{OfferId}/resume - template={Template}", id, dto.TemplateId);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IPipelineRunnerService>();
                await runner.StartGenerationAsync(id, userIdStr, dto.TemplateId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pipeline [GENERATION] task failed for offer {OfferId}", id);
            }
        });

        return Accepted(new { OfferId = id, Status = "generating" });
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
        logger.LogInformation("POST /api/offers/{OfferId}/generate-pdf - template={Template}", id, dto.TemplateId);

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
        var dbUserId = await GetUserIdAsync();
        try
        {
            var result = await offerService.GetAnalysisAsync(dbUserId, id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }


}
