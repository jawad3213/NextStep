using System.Security.Claims;
using backend.Modules.Candidature.DTOs;
using backend.Modules.Candidature.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Modules.Candidature.Controllers;

[ApiController]
[Route("api/candidatures")]
[Authorize]
public class CandidatureController : ControllerBase
{
    private readonly ICandidatureService _candidatureService;

    public CandidatureController(ICandidatureService candidatureService)
    {
        _candidatureService = candidatureService;
    }

    [HttpPost]
    public async Task<ActionResult<CandidatureDto>> Create(
        [FromBody] CreateCandidatureDto dto,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user identifier.");

        var result = await _candidatureService.CreateAsync(userId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidatureDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _candidatureService.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }
}