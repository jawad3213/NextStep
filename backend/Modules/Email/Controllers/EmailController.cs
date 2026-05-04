using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Email.DTOs;
using NextStep.Modules.Email.Services;

namespace NextStep.Modules.Email.Controllers;

[ApiController]
[Route("api/emails")]
[Authorize]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;

    public EmailController(IEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<EmailDraftDto>> GenerateDraft(
        [FromBody] GenerateEmailDraftDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _emailService.GenerateDraftAsync(dto, cancellationToken);
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