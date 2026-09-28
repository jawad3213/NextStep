using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Coaching.Application.Dtos;
using NextStep.Modules.Coaching.Application.Services;
using NextStep.Modules.Profile.Contracts;

namespace NextStep.Modules.Coaching.Api;

[ApiController]
[Route("api/sn")]
[Authorize]
public class SnCopilotController : ControllerBase
{
    private readonly ISnCopilotService _snCopilotService;
    private readonly IProfileApi _profile;
    private readonly ILogger<SnCopilotController> _logger;

    public SnCopilotController(
        ISnCopilotService snCopilotService,
        IProfileApi profile,
        ILogger<SnCopilotController> logger)
    {
        _snCopilotService = snCopilotService;
        _profile = profile;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/sn/chat
    /// Executive chat with SN Copilot to retrieve candidatures, mutate statuses, or get strategic advice.
    /// </summary>
    [HttpPost("chat")]
    public async Task<ActionResult<SnChatAgentResponse>> Chat(
        [FromBody] SnUserChatRequest request,
        CancellationToken cancellationToken)
    {
        var localUserId = await _profile.TryResolveUserIdAsync(User);
        var localUser = localUserId.HasValue ? await _profile.GetUserAsync(localUserId.Value, cancellationToken) : null;
        var userId = localUserId ?? Guid.Empty;
        var userName = localUser?.FullName ?? "Candidat";

        if (userId == Guid.Empty)
        {
            var fallbackSub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(fallbackSub, out var parsedGuid))
            {
                userId = parsedGuid;
            }
            else
            {
                userId = new Guid("00000000-0000-0000-0000-0000000000de");
                if (userName == "Candidat") userName = "Said Nichan";
            }
        }

        return Ok(await _snCopilotService.ChatAsync(userId, userName, request, cancellationToken));
    }

    /// <summary>
    /// GET /api/sn/starter-suggestions
    /// BCG-inspired strategic starter prompts for the empty state.
    /// </summary>
    [HttpGet("starter-suggestions")]
    public async Task<ActionResult<List<SnStarterSuggestionItem>>> GetStarterSuggestions()
    {
        var suggestions = await _snCopilotService.GetStarterSuggestionsAsync();
        return Ok(suggestions);
    }
}
