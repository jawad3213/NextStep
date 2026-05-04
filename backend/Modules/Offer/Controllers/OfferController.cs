// ============================================================
// Modules/Offer/Controllers/OfferController.cs
// Endpoints: POST /api/offers/submit · GET /api/offers/{id}/analysis · GET /api/offers
// ============================================================
using NextStep.Modules.Identity.Repositories;
using NextStep.Modules.Identity.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Identity.Repositories;
using NextStep.Modules.Offer.DTOs;
using NextStep.Modules.Offer.Services;

namespace NextStep.Modules.Offer.Controllers;

[ApiController]
[Route("api/offers")]
[Produces("application/json")]
public class OfferController(
    IOfferService offerService,
    IUserRepository userRepository,
    ILogger<OfferController> logger) : ControllerBase
{
    /// <summary>
    /// Soumettre une offre d'emploi et lancer le pipeline IA.
    /// Angular colle le texte brut → .NET → Python (LangGraph) → JSON complet.
    /// </summary>
    [HttpPost("submit")]
    [Authorize]
    [ProducesResponseType(typeof(OfferAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Submit(
        [FromBody] OfferSubmitDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var localUser = await ResolveLocalUserAsync();
        var userId = localUser?.Id.ToString() ?? "00000000-0000-0000-0000-000000000001";

        logger.LogInformation("POST /api/offers/submit — user={UserId} | template={TemplateId}",
            userId, dto.TemplateId);

        try
        {
            var result = await offerService.SubmitAndAnalyzeAsync(
                dto.RawText, 
                dto.Titre, 
                dto.Entreprise, 
                dto.TemplateId, 
                userId, 
                ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "OfferController — Service IA indisponible");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Récupérer le résultat d'analyse d'une offre déjà traitée.
    /// </summary>
    [HttpGet("{id:guid}/analysis")]
    [ProducesResponseType(typeof(OfferAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysis(Guid id, CancellationToken ct)
    {
        var result = await offerService.GetAnalysisAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Récupérer la liste des offres analysées appartenant à l'utilisateur courant.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<OfferAnalysisDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOffers(CancellationToken ct)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, "User not found in local database.");

        var results = await offerService.GetByUserIdAsync(localUser.Id, ct);
        return Ok(results);
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private async Task<UserEntity?> ResolveLocalUserAsync()
    {
        var keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                      ?? User.FindFirstValue("sub")
                      ?? User.FindFirstValue("uid");

        if (string.IsNullOrWhiteSpace(keycloakId))
            return null;

        return await userRepository.GetByKeycloakIdAsync(keycloakId);
    }
}
