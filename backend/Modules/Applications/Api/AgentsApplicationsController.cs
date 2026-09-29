using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Applications.Api;

/// <summary>
/// Internal endpoints for the Python agents (shared secret, no user token). SN Copilot
/// lists and changes the user's applications here instead of writing the tables itself.
/// <c>userRef</c> is the local user id or the Keycloak subject.
/// </summary>
[ApiController]
[Route("internal/agents/users/{userRef}")]
[InternalApiKey]
[Produces("application/json")]
public class AgentsApplicationsController(IAgentApplicationsService applications, IProfileApi profileApi) : ControllerBase
{
    [HttpGet("candidatures")]
    [ProducesResponseType(typeof(List<AgentCandidatureDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AgentCandidatureDto>>> List(string userRef, [FromQuery] int limit = 50, CancellationToken ct = default) =>
        Ok(await applications.ListAsync(await ResolveAsync(userRef, ct), limit, ct));

    [HttpPost("candidatures")]
    [ProducesResponseType(typeof(AgentCandidatureDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentCandidatureDto>> Create(string userRef, [FromBody] AgentCreateCandidatureDto dto, CancellationToken ct) =>
        Ok(await applications.CreateAsync(await ResolveAsync(userRef, ct), dto, ct));

    [HttpPost("candidatures/{candidatureId:guid}/status")]
    [ProducesResponseType(typeof(AgentCandidatureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentCandidatureDto>> UpdateStatus(string userRef, Guid candidatureId, [FromBody] AgentStatusUpdateDto dto, CancellationToken ct) =>
        Ok(await applications.UpdateStatusAsync(await ResolveAsync(userRef, ct), candidatureId, dto, ct));

    [HttpPost("candidatures/{candidatureId:guid}/notes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddNote(string userRef, Guid candidatureId, [FromBody] AgentNoteDto dto, CancellationToken ct)
    {
        await applications.AddNoteAsync(await ResolveAsync(userRef, ct), candidatureId, dto, ct);
        return NoContent();
    }

    /// <summary>The stored analysis of the user's offer (raw JSON), for the interview coach.</summary>
    [HttpGet("offers/{offerId:guid}/analysis")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOfferAnalysis(string userRef, Guid offerId, CancellationToken ct)
    {
        var json = await applications.GetOfferAnalysisJsonAsync(await ResolveAsync(userRef, ct), offerId, ct)
                   ?? throw new NotFoundException("Analysis not found.");
        return Content(json, "application/json");
    }

    private async Task<Guid> ResolveAsync(string userRef, CancellationToken ct) =>
        await profileApi.FindUserIdAsync(userRef, ct) ?? throw new NotFoundException("User not found.");
}
