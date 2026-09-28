using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Profile.Contracts;
using NextStep.Modules.Sourcing.Application.Dtos;
using NextStep.Modules.Sourcing.Application.Services;
using NextStep.Modules.Sourcing.Infrastructure.JobBoards;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Sourcing.Api;

[ApiController]
[Route("api/sourced-offers")]
[Authorize]
[Produces("application/json")]
public class SourcedOffersController(
    ISourcedOfferService sourcedOfferService,
    IProfileApi profile) : ControllerBase
{
    private async Task<Guid> GetUserIdAsync()
    {
        var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(keycloakId))
        {
            var userId = await profile.EnsureUserIdAsync(User);
            return userId;
        }

        if (Request.Headers.TryGetValue("X-User-Id", out var uid) && Guid.TryParse(uid, out var parsedGuid))
        {
            return parsedGuid;
        }

        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    [HttpPost("search")]
    public async Task<ActionResult<SourcedOfferSearchResponse>> Search([FromBody] SourcedOfferSearchRequest request, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        return Ok(await sourcedOfferService.SearchAsync(userId, request, ct));
    }

    [HttpGet]
    public async Task<ActionResult<List<SourcedOfferListItemDto>>> List(
        [FromQuery] string? keywords,
        [FromQuery] string? location,
        [FromQuery] List<string>? providers,
        [FromQuery] int? limit,
        [FromQuery] string? postedWindow,
        [FromQuery] List<string>? contractTypes,
        [FromQuery] string? indeedCountryCode,
        [FromQuery] string? workflowState,
        CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        var request = new SourcedOfferSearchRequest
        {
            Keywords = keywords,
            Location = location,
            Providers = providers ?? new List<string>(),
            Limit = limit ?? 50,
            PostedWindow = postedWindow ?? PostedWindowValues.Any,
            ContractTypes = contractTypes ?? new List<string>(),
            IndeedCountryCode = indeedCountryCode,
            WorkflowState = workflowState,
        };
        return Ok(await sourcedOfferService.ListAsync(userId, request, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SourcedOfferDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        return Ok(await sourcedOfferService.GetByIdAsync(userId, id, ct)
                  ?? throw new NotFoundException("Offre sourcée introuvable."));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<SourcedOfferDetailDto>> Update(Guid id, [FromBody] SourcedOfferUpdateRequest request, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        return Ok(await sourcedOfferService.UpdateAsync(userId, id, request, ct));
    }

    [HttpPost("{id:guid}/promote")]
    public async Task<ActionResult<PromoteSourcedOfferResponse>> Promote(Guid id, CancellationToken ct)
    {
        var userId = await GetUserIdAsync();
        return Ok(await sourcedOfferService.PromoteAsync(userId, id, ct));
    }
}
