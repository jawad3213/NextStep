using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Email.DTOs;
using NextStep.Modules.Email.Services;
<<<<<<< HEAD
using NextStep.Modules.Identity.Repositories;
=======
using NextStep.Modules.Identity.Services;
>>>>>>> dd6b6da26fce12d583ed2393398e6269c90a5938

namespace NextStep.Modules.Email.Controllers;

[ApiController]
[Route("api/emails")]
[Authorize]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
<<<<<<< HEAD
    private readonly IUserRepository _userRepository;

    public EmailController(IEmailService emailService, IUserRepository userRepository)
    {
        _emailService     = emailService;
        _userRepository   = userRepository;
=======
    private readonly IUserService _userService;

    public EmailController(IEmailService emailService, IUserService userService)
    {
        _emailService = emailService;
        _userService = userService;
>>>>>>> dd6b6da26fce12d583ed2393398e6269c90a5938
    }

    // ── Existing: Generate draft ──────────────────────────────────────────────────

    [HttpPost("generate")]
    public async Task<ActionResult<EmailDraftDto>> GenerateDraft(
        [FromBody] GenerateEmailDraftDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _emailService.GenerateDraftAsync(dto, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

<<<<<<< HEAD
    // ── Existing: Get drafts by candidature ───────────────────────────────────────
=======
    [HttpPost("send")]
    public async Task<ActionResult<EmailDraftDto>> SendApplicationEmail(
        [FromBody] SendApplicationEmailDto dto,
        CancellationToken cancellationToken)
    {
        var user = await _userService.EnsureUserCreatedAsync(User);
        var result = await _emailService.SendApplicationEmailAsync(user.Id, dto, cancellationToken);
        return Ok(result);
    }
>>>>>>> dd6b6da26fce12d583ed2393398e6269c90a5938

    [HttpGet("candidature/{candidatureId:guid}")]
    public async Task<ActionResult<List<EmailDraftDto>>> GetByCandidature(
        Guid candidatureId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _emailService.GetDraftsByCandidatureAsync(
                candidatureId, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ── Existing: Get draft by ID ───────────────────────────────────────
    [HttpGet("drafts/{draftId:guid}")]
    public async Task<ActionResult<EmailDraftDto>> GetDraftById(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return Unauthorized("User not found in local database.");

        try
        {
            var result = await _emailService.GetDraftByIdAsync(draftId, localUserId.Value, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // ── POST /api/emails/generate-follow-up ──────────────────────────────────────
    [HttpPost("generate-follow-up")]
    public async Task<ActionResult<EmailDraftDto>> GenerateFollowUpDraft(
        [FromBody] GenerateFollowUpDraftDto dto,
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return Unauthorized("User not found in local database.");

        try
        {
            var result = await _emailService.GenerateFollowUpDraftAsync(dto, localUserId.Value, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // ── POST /api/emails/generate-reply ──────────────────────────────────────────

    [HttpPost("generate-reply")]
    public async Task<ActionResult<EmailDraftDto>> GenerateReplyDraft(
        [FromBody] GenerateReplyDraftDto dto,
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return Unauthorized("User not found in local database.");

        try
        {
            var result = await _emailService.GenerateReplyDraftAsync(dto, localUserId.Value, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // ── PUT /api/emails/drafts/{draftId} — Update draft ──────────────────────────

    [HttpPut("drafts/{draftId:guid}")]
    public async Task<ActionResult<EmailDraftDto>> UpdateDraft(
        Guid draftId,
        [FromBody] UpdateEmailDraftDto dto,
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        try
        {
            var result = await _emailService.UpdateDraftAsync(
                draftId, localUserId.Value, dto, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ── POST /api/emails/drafts/{draftId}/approve — Approve draft ────────────────

    [HttpPost("drafts/{draftId:guid}/approve")]
    public async Task<ActionResult<EmailDraftDto>> ApproveDraft(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        try
        {
            var result = await _emailService.ApproveDraftAsync(
                draftId, localUserId.Value, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ── POST /api/emails/drafts/{draftId}/send — Send approved draft ──────────────

    [HttpPost("drafts/{draftId:guid}/send")]
    public async Task<ActionResult<SendEmailResultDto>> SendDraft(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        try
        {
            var result = await _emailService.SendDraftAsync(
                draftId, localUserId.Value, cancellationToken);

            // Return 200 regardless of success/failure — the result contains the status.
            // The caller must check result.Success.
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<Guid?> ResolveLocalUserIdAsync()
    {
        // Try 'sub' first, then NameIdentifier
        var keycloakId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrWhiteSpace(keycloakId))
        {
            return null;
        }

        var user = await _userRepository.GetByKeycloakIdAsync(keycloakId);
        return user?.Id;
    }
}
