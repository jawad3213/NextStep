// ============================================================
// Modules/Offer/Controllers/OfferController.cs
// Endpoints : POST /api/offers/submit · GET /api/offers/{id}/analysis
// ============================================================
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Offer.DTOs;
using NextStep.Modules.Offer.Services;

namespace NextStep.Modules.Offer.Controllers;

[ApiController]
[Route("api/offers")]
[Produces("application/json")]
public class OfferController(IOfferService offerService, ILogger<OfferController> logger) : ControllerBase
{
    /// <summary>
    /// Soumettre une offre d'emploi et lancer le pipeline IA.
    /// Angular colle le texte brut → .NET → Python (LangGraph) → JSON complet.
    /// </summary>
    /// <param name="dto">Texte brut de l'offre + ID template CV</param>
    /// <returns>Résultat d'analyse : titre, compétences, scores ATS/matching, email stub</returns>
    [HttpPost("submit")]
    [ProducesResponseType(typeof(OfferAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Submit(
        [FromBody] OfferSubmitDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // TODO(M1) : Récupérer le vrai user_id depuis le JWT Keycloak
        // Pour l'instant, userId est extrait d'un header de test ou d'une valeur par défaut
        var userId = Request.Headers.TryGetValue("X-User-Id", out var uid) && !string.IsNullOrEmpty(uid)
            ? uid.ToString()
            : "00000000-0000-0000-0000-000000000001"; // Valeur de test

        logger.LogInformation("POST /api/offers/submit — user={UserId} | template={TemplateId}",
            userId, dto.TemplateId);

        try
        {
            var result = await offerService.SubmitAndAnalyzeAsync(dto.RawText, dto.TemplateId, userId, ct);
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
    /// <param name="id">ID de l'offre (UUID)</param>
    [HttpGet("{id:guid}/analysis")]
    [ProducesResponseType(typeof(OfferAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysis(Guid id, CancellationToken ct)
    {
        var result = await offerService.GetAnalysisAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
