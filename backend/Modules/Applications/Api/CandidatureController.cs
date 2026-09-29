using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Pagination;

namespace NextStep.Modules.Applications.Api;

[ApiController]
[Route("api/candidatures")]
[Authorize]
public class CandidatureController(ICandidatureService candidatureService, IProfileApi profile) : ControllerBase
{
    private async Task<Guid> RequireUserIdAsync() =>
        await profile.TryResolveUserIdAsync(User)
        ?? throw new ForbiddenException("User not found.");

    // ── POST /api/candidatures — Create ─────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<CandidatureDto>> Create(
        [FromBody] CreateCandidatureDto dto,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync();
        var result = await candidatureService.CreateAsync(userId, dto, cancellationToken: cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.IdCandidature }, result);
    }

    // ── GET /api/candidatures — List current user's candidatures ─────────────────

    [HttpGet]
    public async Task<ActionResult<List<CandidatureDto>>> GetMyCandidatures(CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync();
        return Ok(await candidatureService.GetByUserIdAsync(userId, cancellationToken));
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResponse<CandidatureDto>>> GetMyCandidaturesPaged(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 10,
        [FromQuery] bool interviewOnly = false,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        return Ok(await candidatureService.GetByUserIdPagedAsync(userId, offset, limit, interviewOnly, cancellationToken));
    }

    // ── GET /api/candidatures/{id} — Get by ID with ownership check ──────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidatureDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync();
        return Ok(await candidatureService.GetOwnedAsync(userId, id, cancellationToken));
    }

    // ── PATCH /api/candidatures/{id}/statut — Change status ─────────────────────

    [HttpPatch("{id:guid}/statut")]
    public async Task<ActionResult<CandidatureDto>> UpdateStatut(
        Guid id,
        [FromBody] UpdateStatutDto dto,
        CancellationToken cancellationToken)
    {
        await candidatureService.GetOwnedAsync(await RequireUserIdAsync(), id, cancellationToken);
        return Ok(await candidatureService.UpdateStatutAsync(id, dto, cancellationToken: cancellationToken)
                  ?? throw new NotFoundException("Application not found."));
    }

    // ── PUT /api/candidatures/{id} — Update candidature fields ──────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CandidatureDto>> Update(
        Guid id,
        [FromBody] UpdateCandidatureDto dto,
        CancellationToken cancellationToken)
    {
        await candidatureService.GetOwnedAsync(await RequireUserIdAsync(), id, cancellationToken);
        return Ok(await candidatureService.UpdateAsync(id, dto, cancellationToken)
                  ?? throw new NotFoundException("Application not found."));
    }

    // ── DELETE /api/candidatures/{id} — Delete candidature ──────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await candidatureService.GetOwnedAsync(await RequireUserIdAsync(), id, cancellationToken);
        if (!await candidatureService.DeleteAsync(id, cancellationToken))
            throw new NotFoundException("Application not found.");

        return NoContent();
    }

    // ── POST /api/candidatures/{id}/notes — Add a note ─────────────────────────

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<CandidatureNoteDto>> AddNote(
        Guid id,
        [FromBody] AddNoteDto dto,
        CancellationToken cancellationToken)
    {
        await candidatureService.GetOwnedAsync(await RequireUserIdAsync(), id, cancellationToken);
        var note = await candidatureService.AddNoteAsync(id, dto, cancellationToken);
        return CreatedAtAction(nameof(GetNotes), new { id }, note);
    }

    // ── GET /api/candidatures/{id}/notes — List notes ──────────────────────────

    [HttpGet("{id:guid}/notes")]
    public async Task<ActionResult<List<CandidatureNoteDto>>> GetNotes(Guid id, CancellationToken cancellationToken)
    {
        await candidatureService.GetOwnedAsync(await RequireUserIdAsync(), id, cancellationToken);
        return Ok(await candidatureService.GetNotesAsync(id, cancellationToken));
    }

    // ── GET /api/candidatures/{id}/history — Status change history ──────────────

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<CandidatureStatusHistoryDto>>> GetHistory(Guid id, CancellationToken cancellationToken)
    {
        await candidatureService.GetOwnedAsync(await RequireUserIdAsync(), id, cancellationToken);
        return Ok(await candidatureService.GetHistoryAsync(id, cancellationToken));
    }
}
