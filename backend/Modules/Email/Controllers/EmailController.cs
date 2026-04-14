using backend.Modules.Email.DTOs;
using backend.Modules.Email.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Modules.Email.Controllers;

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
}