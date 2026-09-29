using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Profile.Application.Dtos;
using NextStep.Modules.Profile.Application.Services;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Profile.Api;

/// <summary>
/// Internal endpoints for the Python agents (shared secret, no user token): the agents
/// read profiles through the Profile module instead of querying its tables.
/// </summary>
[ApiController]
[Route("internal/agents")]
[InternalApiKey]
[Produces("application/json")]
public class AgentsProfileController(IProfileApi profileApi, IProfileService profileService) : ControllerBase
{
    /// <param name="userRef">Local user id or Keycloak subject.</param>
    [HttpGet("users/{userRef}")]
    [ProducesResponseType(typeof(AgentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentUserDto>> GetUser(string userRef, CancellationToken ct)
    {
        var userId = await ResolveAsync(userRef, ct);
        var user = await profileApi.GetUserAsync(userId, ct) ?? throw new NotFoundException("User not found.");
        return Ok(new AgentUserDto(user.UserId, user.FirstName, user.LastName, user.Email));
    }

    /// <param name="userRef">Local user id or Keycloak subject.</param>
    [HttpGet("profiles/{userRef}")]
    [ProducesResponseType(typeof(AgentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentProfileDto>> GetProfile(string userRef, CancellationToken ct)
    {
        var userId = await ResolveAsync(userRef, ct);
        return Ok(new AgentProfileDto(userId, await profileService.GetFullProfileAsync(userId)));
    }

    private async Task<Guid> ResolveAsync(string userRef, CancellationToken ct) =>
        await profileApi.FindUserIdAsync(userRef, ct) ?? throw new NotFoundException("User not found.");
}
