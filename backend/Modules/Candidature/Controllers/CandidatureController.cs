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
            return StatusCode(403, new { error = "User not found in local database." });

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
            return StatusCode(403, new { error = "User not found in local database." });

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
            return StatusCode(403, new { error = "User not found in local database." });

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
            return StatusCode(403, new { error = "User not found in local database." });

        var result = await _candidatureService.GetByIdAsync(id, cancellationToken);

        if (result is null)
            return NotFound();

        if (result.IdUtilisateur != localUser.Id)
            return StatusCode(403, new { error = "You do not have access to this candidature." });

        return Ok(result);
    }

    // ── PATCH /api/candidatures/{id}/statut — Change status ─────────────────────

    [HttpPatch("{id:guid}/statut")]
    public async Task<ActionResult<CandidatureDto>> UpdateStatut(
        Guid id,
        [FromBody] UpdateStatutDto dto,
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, new { error = "User not found in local database." });

        var existing = await _candidatureService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        if (existing.IdUtilisateur != localUser.Id)
            return StatusCode(403, new { error = "You do not have access to this candidature." });

        var result = await _candidatureService.UpdateStatutAsync(id, dto, cancellationToken);
        if (result is null) return NotFound();

        return Ok(result);
    }

    // ── PUT /api/candidatures/{id} — Update candidature fields ──────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CandidatureDto>> Update(
        Guid id,
        [FromBody] UpdateCandidatureDto dto,
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, new { error = "User not found in local database." });

        var existing = await _candidatureService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        if (existing.IdUtilisateur != localUser.Id)
            return StatusCode(403, new { error = "You do not have access to this candidature." });

        var result = await _candidatureService.UpdateAsync(id, dto, cancellationToken);
        if (result is null) return NotFound();

        return Ok(result);
    }

    // ── DELETE /api/candidatures/{id} — Delete candidature ──────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, new { error = "User not found in local database." });

        var existing = await _candidatureService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        if (existing.IdUtilisateur != localUser.Id)
            return StatusCode(403, new { error = "You do not have access to this candidature." });

        var deleted = await _candidatureService.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound();

        return NoContent();
    }

    // ── POST /api/candidatures/{id}/notes — Add a note ─────────────────────────

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<CandidatureNoteDto>> AddNote(
        Guid id,
        [FromBody] AddNoteDto dto,
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, new { error = "User not found in local database." });

        var existing = await _candidatureService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        if (existing.IdUtilisateur != localUser.Id)
            return StatusCode(403, new { error = "You do not have access to this candidature." });

        var note = await _candidatureService.AddNoteAsync(id, dto, cancellationToken);

        return CreatedAtAction(nameof(GetNotes), new { id }, note);
    }

    // ── GET /api/candidatures/{id}/notes — List notes ──────────────────────────

    [HttpGet("{id:guid}/notes")]
    public async Task<ActionResult<List<CandidatureNoteDto>>> GetNotes(
        Guid id,
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, new { error = "User not found in local database." });

        var existing = await _candidatureService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        if (existing.IdUtilisateur != localUser.Id)
            return StatusCode(403, new { error = "You do not have access to this candidature." });

        var notes = await _candidatureService.GetNotesAsync(id, cancellationToken);
        return Ok(notes);
    }

    // ── GET /api/candidatures/{id}/history — Status change history ──────────────

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<CandidatureStatusHistoryDto>>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var localUser = await ResolveLocalUserAsync();
        if (localUser is null)
            return StatusCode(403, new { error = "User not found in local database." });

        var existing = await _candidatureService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        if (existing.IdUtilisateur != localUser.Id)
            return StatusCode(403, new { error = "You do not have access to this candidature." });

        var history = await _candidatureService.GetHistoryAsync(id, cancellationToken);
        return Ok(history);
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
