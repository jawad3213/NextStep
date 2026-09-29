using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Shared.Http;
using System.Text.Json;
using NextStep.Modules.Sourcing.Application.Dtos;

namespace NextStep.Modules.Sourcing.Api;

/// <summary>
/// Proxy to the Python agents service (FastAPI). Centralizes AI calls from the
/// frontend behind the backend API: single origin, simplified CORS, errors
/// normalized via the common error contract. In local mode these endpoints
/// fail gracefully (502) if the agents service is not started.
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
            // Re-serialize the agent JSON into the response, as-is.
            return Content(doc.RootElement.GetRawText(), "application/json");
        }
        catch (HttpRequestException ex)
        {
            // The agents service is unavailable or returned an error.
            // We keep an appropriate application status (502 = upstream gateway).
            _logger.LogWarning(ex, "AgentsProxy — upstream error on {Path}.", path);
            return StatusCode(502, new AgentsProxyErrorResponse(
                "The AI service is temporarily unavailable.", ex.Message, path));
        }
    }
}
