using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Email.DTOs;
using NextStep.Modules.Email.Services;
using NextStep.Modules.Identity.Services;

namespace NextStep.Modules.Email.Controllers;

[ApiController]
[Route("api/emails")]
[Authorize]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IUserService _userService;

    public EmailController(IEmailService emailService, IUserService userService)
    {
        _emailService = emailService;
        _userService = userService;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<EmailDraftDto>> GenerateDraft(
        [FromBody] GenerateEmailDraftDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _emailService.GenerateDraftAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPost("send")]
    public async Task<ActionResult<EmailDraftDto>> SendApplicationEmail(
        [FromBody] SendApplicationEmailDto dto,
        CancellationToken cancellationToken)
    {
        var user = await _userService.EnsureUserCreatedAsync(User);
        var result = await _emailService.SendApplicationEmailAsync(user.Id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpGet("candidature/{candidatureId:guid}")]
    public async Task<ActionResult<List<EmailDraftDto>>> GetByCandidature(
        Guid candidatureId,
        CancellationToken cancellationToken)
    {
        var result = await _emailService.GetDraftsByCandidatureAsync(
            candidatureId,
            cancellationToken);

        return Ok(result);
    }
}
