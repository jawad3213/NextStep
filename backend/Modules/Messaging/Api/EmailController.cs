using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Messaging.Application.Dtos;
using NextStep.Modules.Messaging.Application.Services;
using NextStep.Modules.Messaging.Infrastructure.Gmail;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Messaging.Api;

[ApiController]
[Route("api/emails")]
[Authorize]
public class EmailController(
    IEmailDraftService drafts,
    IEmailSendingService sending,
    IFollowUpEmailService followUps,
    IReplyEmailService replies,
    IProfileApi profile) : ControllerBase
{
    // ── Drafts ──────────────────────────────────────────────────────────────

    [HttpPost("generate")]
    public async Task<ActionResult<EmailDraftDto>> GenerateDraft(
        [FromBody] GenerateEmailDraftDto dto,
        CancellationToken cancellationToken) =>
        Ok(await drafts.GenerateDraftAsync(await RequireLocalUserIdAsync(), dto, cancellationToken));

    [HttpGet("candidature/{candidatureId:guid}")]
    public async Task<ActionResult<List<EmailDraftDto>>> GetByCandidature(
        Guid candidatureId,
        CancellationToken cancellationToken) =>
        Ok(await drafts.GetDraftsByCandidatureAsync(candidatureId, await RequireLocalUserIdAsync(), cancellationToken));

    [HttpGet("drafts/{draftId:guid}")]
    public async Task<ActionResult<EmailDraftDto>> GetDraftById(
        Guid draftId,
        CancellationToken cancellationToken) =>
        Ok(await drafts.GetDraftByIdAsync(draftId, await RequireLocalUserIdAsync(), cancellationToken));

    [HttpPut("drafts/{draftId:guid}")]
    public async Task<ActionResult<EmailDraftDto>> UpdateDraft(
        Guid draftId,
        [FromBody] UpdateEmailDraftDto dto,
        CancellationToken cancellationToken) =>
        Ok(await drafts.UpdateDraftAsync(draftId, await RequireLocalUserIdAsync(), dto, cancellationToken));

    [HttpPost("drafts/{draftId:guid}/approve")]
    public async Task<ActionResult<EmailDraftDto>> ApproveDraft(
        Guid draftId,
        CancellationToken cancellationToken) =>
        Ok(await drafts.ApproveDraftAsync(draftId, await RequireLocalUserIdAsync(), cancellationToken));

    // ── Follow-ups and replies ──────────────────────────────────────────────

    [HttpPost("generate-follow-up")]
    public async Task<ActionResult<EmailDraftDto>> GenerateFollowUpDraft(
        [FromBody] GenerateFollowUpDraftDto dto,
        CancellationToken cancellationToken) =>
        Ok(await followUps.GenerateFollowUpDraftAsync(dto, await RequireLocalUserIdAsync(), cancellationToken));

    [HttpPost("generate-reply")]
    public async Task<ActionResult<EmailDraftDto>> GenerateReplyDraft(
        [FromBody] GenerateReplyDraftDto dto,
        CancellationToken cancellationToken) =>
        Ok(await replies.GenerateReplyDraftAsync(dto, await RequireLocalUserIdAsync(), cancellationToken));

    // ── Sending ─────────────────────────────────────────────────────────────

    [HttpPost("send")]
    public async Task<ActionResult<EmailDraftDto>> SendApplicationEmail(
        [FromBody] SendApplicationEmailDto dto,
        CancellationToken cancellationToken)
    {
        var userId = await profile.EnsureUserIdAsync(User);
        return Ok(await sending.SendApplicationEmailAsync(userId, dto, cancellationToken));
    }

    /// <summary>
    /// Returns 200 regardless of success/failure — the result contains the status.
    /// The caller must check result.Success.
    /// </summary>
    [HttpPost("drafts/{draftId:guid}/send")]
    public async Task<ActionResult<SendEmailResultDto>> SendDraft(
        Guid draftId,
        CancellationToken cancellationToken) =>
        Ok(await sending.SendDraftAsync(draftId, await RequireLocalUserIdAsync(), cancellationToken));

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<Guid> RequireLocalUserIdAsync()
    {
        var keycloakId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = string.IsNullOrWhiteSpace(keycloakId) ? null : await profile.FindUserIdAsync(keycloakId);
        return userId ?? throw new ForbiddenException("User not found in local database.");
    }
}
