using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Chatbot.DTOs;
using NextStep.Modules.Chatbot.Interfaces;
using NextStep.Modules.Identity.Models;
using NextStep.Modules.Identity.Repositories;
using NextStep.Modules.Identity.Services;

namespace NextStep.Modules.Chatbot.Controllers;

[ApiController]
[Route("api/sn")]
[Authorize]
public class SnCopilotController : ControllerBase
{
    private readonly ISnCopilotService _snCopilotService;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SnCopilotController> _logger;

    public SnCopilotController(
        ISnCopilotService snCopilotService,
        IUserService userService,
        IUserRepository userRepository,
        ILogger<SnCopilotController> logger)
    {
        _snCopilotService = snCopilotService;
        _userService = userService;
        _userRepository = userRepository;
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
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Le message ne peut pas être vide." });
        }

        var localUser = await ResolveLocalUserAsync();
        var userId = localUser?.Id ?? Guid.Empty;
        var userName = localUser != null ? $"{localUser.Prenom} {localUser.Nom}".Trim() : "Candidat";

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

        try
        {
            var response = await _snCopilotService.ChatAsync(userId, userName, request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'interaction avec SN Copilot.");
            return StatusCode(500, new { error = "Erreur de communication avec le copilote SN." });
        }
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

    private async Task<UserEntity?> ResolveLocalUserAsync()
    {
        try
        {
            return await _userService.EnsureUserCreatedAsync(User);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnsureUserCreatedAsync failed, falling back to direct Keycloak lookup");
            var keycloakId = User.FindFirstValue("sub")
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("uid");

            if (string.IsNullOrWhiteSpace(keycloakId))
                return null;

            return await _userRepository.GetByKeycloakIdAsync(keycloakId);
        }
    }
}
