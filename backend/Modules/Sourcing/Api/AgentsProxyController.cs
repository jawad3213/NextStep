using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Shared.Http;
using System.Text.Json;
using NextStep.Modules.Sourcing.Application.Dtos;

namespace NextStep.Modules.Sourcing.Api;

/// <summary>
/// Proxy vers le service d'agents Python (FastAPI). Centralise les appels IA du
/// frontend derrière l'API backend : une seule origine, CORS simplifié, erreurs
/// normalisées via le contrat d'erreur commun. En mode local ces endpoints
/// échouent proprement (502) si le service d'agents n'est pas démarré.
/// </summary>
[ApiController]
[Route("api/agents")]
[Authorize]
public class AgentsProxyController : ControllerBase
{
    private readonly IAgentHttpClient _agentClient;
    private readonly ILogger<AgentsProxyController> _logger;

    public AgentsProxyController(IAgentHttpClient agentClient, ILogger<AgentsProxyController> logger)
    {
        _agentClient = agentClient;
        _logger = logger;
    }

    [HttpPost("company/analyze")]
    public async Task<IActionResult> AnalyzeCompany([FromBody] JsonElement body, CancellationToken ct)
    {
        return await ForwardAsync("/company/analyze-company", body, ct);
    }

    [HttpPost("jobs/linkedin/search")]
    public async Task<IActionResult> SearchLinkedIn([FromBody] JsonElement body, CancellationToken ct)
    {
        return await ForwardAsync("/linkedin-jobs/search", body, ct);
    }

    [HttpPost("jobs/indeed/search")]
    public async Task<IActionResult> SearchIndeed([FromBody] JsonElement body, CancellationToken ct)
    {
        return await ForwardAsync("/indeed-jobs/search", body, ct);
    }

    [HttpPost("jobs/glassdoor/search")]
    public async Task<IActionResult> SearchGlassdoor([FromBody] JsonElement body, CancellationToken ct)
    {
        return await ForwardAsync("/glassdoor-jobs/search", body, ct);
    }

    private async Task<IActionResult> ForwardAsync(string path, JsonElement body, CancellationToken ct)
    {
        try
        {
            var doc = await _agentClient.PostRawAsync(path, body, ct);
            // Re-sérialise le JSON des agents dans la réponse, tel quel.
            return Content(doc.RootElement.GetRawText(), "application/json");
        }
        catch (HttpRequestException ex)
        {
            // Le service d'agents est indisponible ou a renvoyé une erreur.
            // On garde un statut applicatif approprié (502 = passerelle amont).
            _logger.LogWarning(ex, "AgentsProxy — erreur amont sur {Path}.", path);
            return StatusCode(502, new AgentsProxyErrorResponse(
                "Le service IA est temporairement indisponible.", ex.Message, path));
        }
    }
}
