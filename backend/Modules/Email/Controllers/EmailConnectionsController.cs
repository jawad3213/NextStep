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
    private const string FrontendBaseUrlConfigKey = "App:FrontendBaseUrl";

    private readonly IEmailConnectionService _connectionService;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailConnectionsController> _logger;

    public EmailConnectionsController(
        IEmailConnectionService connectionService,
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<EmailConnectionsController> logger)
    {
        _connectionService = connectionService;
        _userRepository    = userRepository;
        _configuration     = configuration;
        _logger            = logger;
    }
    [HttpPost("google/credentials")]
    [Authorize]
    public async Task<IActionResult> SaveGoogleCredentials(
        [FromBody] SaveGoogleClientCredentialsDto dto,
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        try
        {
            await _connectionService.SaveGoogleClientCredentialsAsync(localUserId.Value, dto, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("google/credentials")]
    [Authorize]
    public async Task<ActionResult<GoogleClientCredentialsSummaryDto>> GetGoogleCredentialsSummary(
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        var summary = await _connectionService.GetGoogleClientCredentialsSummaryAsync(localUserId.Value, cancellationToken);
        return Ok(summary);
    }

    [HttpDelete("google/credentials")]
    [Authorize]
    public async Task<IActionResult> DeleteGoogleCredentials(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        await _connectionService.DeleteGoogleClientCredentialsAsync(localUserId.Value, cancellationToken);
        return NoContent();
    }
    // GET /api/email-connections/google/login â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            _logger.LogError(ex, "EmailConnections â€” failed to build Google login URL for user {UserId}", localUserId);
            return StatusCode(500, $"Configuration error: {ex.Message}");
        }
    }
    // GET /api/email-connections/google/login-url â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            _logger.LogError(ex, "EmailConnections â€” failed to build Google login URL for user {UserId}", localUserId);
            return StatusCode(500, $"Configuration error: {ex.Message}");
        }
    }

    // â”€â”€ GET /api/email-connections/google/callback â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

            return Redirect(BuildFrontendOAuthRedirectUrl(success: true));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "EmailConnections â€” OAuth callback failed");
            return Redirect(BuildFrontendOAuthRedirectUrl(success: false, error: ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailConnections â€” unexpected error in OAuth callback");
            return Redirect(BuildFrontendOAuthRedirectUrl(
                success: false,
                error: "An unexpected error occurred during Gmail connection."));
        }
    }

    // â”€â”€ GET /api/email-connections/status â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ DELETE /api/email-connections â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    
    /// <summary>
    /// Disconnects the Gmail account for the authenticated user.
    /// </summary>
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        await _connectionService.DisconnectAsync(localUserId.Value, cancellationToken);
        return NoContent();
    }

    // â”€â”€ POST /api/email-connections/verify â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Deep-verifies the Gmail connection by trying to refresh tokens.
    /// </summary>
    [HttpPost("verify")]
    [Authorize]
    public async Task<ActionResult<EmailConnectionStatusDto>> Verify(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return StatusCode(403, "User not found in local database.");

        var status = await _connectionService.VerifyConnectionAsync(localUserId.Value, cancellationToken);
        return Ok(status);
    }

    // â”€â”€ Private helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<Guid?> ResolveLocalUserIdAsync()
    {
        // Try 'sub' first, then NameIdentifier
        var keycloakId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrWhiteSpace(keycloakId))
        {
            _logger.LogWarning("EmailConnections â€” No 'sub' or 'NameIdentifier' claim found in token.");
            return null;
        }

        var user = await _userRepository.GetByKeycloakIdAsync(keycloakId);
        
        if (user == null)
        {
            _logger.LogWarning("EmailConnections â€” User with Keycloak ID {KeycloakId} not found in local database.", keycloakId);
        }

        return user?.Id;
    }

    private string BuildFrontendOAuthRedirectUrl(bool success, string? error = null)
    {
        var baseUrl = _configuration[FrontendBaseUrlConfigKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:4200";
        }

        var normalizedBase = baseUrl.TrimEnd('/');
        var status = success ? "success" : "error";
        var encodedError = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"&gmailError={Uri.EscapeDataString(error)}";

        return $"{normalizedBase}/settings?section=gmail&gmailOAuth={status}{encodedError}";
    }
}

