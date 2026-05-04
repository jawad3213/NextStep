using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Email.DTOs;
using NextStep.Modules.Email.Services;
using NextStep.Modules.Identity.Repositories;

namespace NextStep.Modules.Email.Controllers;

[ApiController]
[Route("api/email-connections")]
public class EmailConnectionsController : ControllerBase
{
    private readonly IEmailConnectionService _connectionService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<EmailConnectionsController> _logger;

    public EmailConnectionsController(
        IEmailConnectionService connectionService,
        IUserRepository userRepository,
        ILogger<EmailConnectionsController> logger)
    {
        _connectionService = connectionService;
        _userRepository    = userRepository;
        _logger            = logger;
    }

    // ── GET /api/email-connections/google/login ───────────────────────────────────

    /// <summary>
    /// Initiates the Gmail OAuth flow for the current authenticated user.
    /// Returns a 302 redirect to Google's authorization page.
    /// The Bearer token must be included (call from frontend or Swagger with auth).
    /// </summary>
    [HttpGet("google/login")]
    [Authorize]
    public async Task<IActionResult> GoogleLogin(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User authenticated but not found in local database. Please complete onboarding.");

        try
        {
            var loginUrl = await _connectionService.GetGoogleLoginUrlAsync(
                localUserId.Value, cancellationToken);

            return Redirect(loginUrl);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "EmailConnections — failed to build Google login URL for user {UserId}", localUserId);
            return StatusCode(500, $"Configuration error: {ex.Message}");
        }
    }

    // ── GET /api/email-connections/google/login-url ────────────────────────────────

    /// <summary>
    /// Initiates the Gmail OAuth flow for the current authenticated user.
    /// Returns the Google authorization page URL to be navigated to by the frontend.
    /// </summary>
    [HttpGet("google/login-url")]
    [Authorize]
    public async Task<IActionResult> GetGoogleLoginUrl(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User authenticated but not found in local database. Please complete onboarding.");

        try
        {
            var loginUrl = await _connectionService.GetGoogleLoginUrlAsync(
                localUserId.Value, cancellationToken);

            return Ok(new { url = loginUrl });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "EmailConnections — failed to build Google login URL for user {UserId}", localUserId);
            return StatusCode(500, $"Configuration error: {ex.Message}");
        }
    }

    // ── GET /api/email-connections/google/callback ────────────────────────────────

    /// <summary>
    /// Handles the Google OAuth redirect callback.
    /// This endpoint is [AllowAnonymous] because Google redirects the user's browser here
    /// without the Bearer token. Security is provided by the signed OAuthState validation.
    /// </summary>
    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest("Missing 'code' or 'state' query parameters.");
        }

        try
        {
            await _connectionService.HandleGoogleCallbackAsync(code, state, cancellationToken);

            // Redirect to the frontend test page after successful connection.
            return Redirect("http://localhost:4200/email-test");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "EmailConnections — OAuth callback failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailConnections — unexpected error in OAuth callback");
            return StatusCode(500, "An unexpected error occurred during Gmail connection.");
        }
    }

    // ── GET /api/email-connections/status ─────────────────────────────────────────

    /// <summary>
    /// Returns the current Gmail connection status for the authenticated user.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<EmailConnectionStatusDto>> GetStatus(
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        var status = await _connectionService.GetStatusAsync(localUserId.Value, cancellationToken);
        return Ok(status);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<Guid?> ResolveLocalUserIdAsync()
    {
        // Try 'sub' first, then NameIdentifier
        var keycloakId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrWhiteSpace(keycloakId))
        {
            _logger.LogWarning("EmailConnections — No 'sub' or 'NameIdentifier' claim found in token.");
            return null;
        }

        var user = await _userRepository.GetByKeycloakIdAsync(keycloakId);
        
        if (user == null)
        {
            _logger.LogWarning("EmailConnections — User with Keycloak ID {KeycloakId} not found in local database.", keycloakId);
        }

        return user?.Id;
    }
}
