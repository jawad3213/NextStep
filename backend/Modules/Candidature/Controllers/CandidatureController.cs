using NextStep.Modules.Identity.Repositories;
using NextStep.Modules.Identity.Models;
using NextStep.Modules.Identity.Services;
using System.Security.Claims;
using NextStep.Modules.Candidature.DTOs;
using NextStep.Modules.Candidature.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NextStep.Modules.Candidature.Controllers;

[ApiController]
[Route("api/candidatures")]
[Authorize]
public class CandidatureController : ControllerBase
{
    private readonly ICandidatureService _candidatureService;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;

    public CandidatureController(
        ICandidatureService candidatureService,
        IUserRepository userRepository,
        IUserService userService)
    {
        _candidatureService = candidatureService;
        _userRepository = userRepository;
        _userService = userService;
    }

    // ── POST /api/candidatures — Create ─────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<CandidatureDto>> Create(
        [FromBody] CreateCandidatureDto dto,
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, "User not found in local database.");

        var result = await _candidatureService.CreateAsync(localUser.Id, dto, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.IdCandidature }, result);
    }

    // ── GET /api/candidatures — List current user's candidatures ─────────────────

    [HttpGet]
    public async Task<ActionResult<List<CandidatureDto>>> GetMyCandidatures(
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, "User not found in local database.");

        var results = await _candidatureService.GetByUserIdAsync(localUser.Id, cancellationToken);
        return Ok(results);
    }

    [HttpGet("paged")]
    public async Task<ActionResult> GetMyCandidaturesPaged(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 10,
        [FromQuery] bool interviewOnly = false,
        CancellationToken cancellationToken = default)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, "User not found in local database.");

        var page = await _candidatureService.GetByUserIdPagedAsync(
            localUser.Id,
            offset,
            limit,
            interviewOnly,
            cancellationToken);

        return Ok(page);
    }

    // ── GET /api/candidatures/{id} — Get by ID with ownership check ──────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidatureDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, "User not found in local database.");

        var result = await _candidatureService.GetByIdAsync(id, cancellationToken);

        if (result is null)
            return NotFound();

        // Ownership check — candidature must belong to current user
        if (result.IdUtilisateur != localUser.Id)
            return StatusCode(403, "You do not have access to this candidature.");

        return Ok(result);
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private async Task<UserEntity?> ResolveLocalUserAsync()
    {
        try
        {
            return await _userService.EnsureUserCreatedAsync(User);
        }
        catch
        {
            var keycloakId = User.FindFirstValue("sub")
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("uid");

            if (string.IsNullOrWhiteSpace(keycloakId))
                return null;

            return await _userRepository.GetByKeycloakIdAsync(keycloakId);
        }
    }
}
