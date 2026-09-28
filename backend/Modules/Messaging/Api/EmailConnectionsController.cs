using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Messaging.Application.Dtos;
using NextStep.Shared.ErrorHandling;
using NextStep.Modules.Messaging.Application.Services;
using NextStep.Modules.Messaging.Infrastructure.Gmail;
using NextStep.Modules.Profile.Contracts;

namespace NextStep.Modules.Messaging.Api;

[ApiController]
[Route("api/email-connections")]
public class EmailConnectionsController : ControllerBase
{
    private const string FrontendBaseUrlConfigKey = "App:FrontendBaseUrl";

    private readonly IEmailConnectionService _connectionService;
    private readonly IProfileApi _profile;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailConnectionsController> _logger;

    public EmailConnectionsController(
        IEmailConnectionService connectionService,
        IProfileApi profile,
        IConfiguration configuration,
        ILogger<EmailConnectionsController> logger)
    {
        _connectionService = connectionService;
        _profile           = profile;
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
            return ApiResult.Forbidden("Utilisateur introuvable.");

        await _connectionService.SaveGoogleClientCredentialsAsync(localUserId.Value, dto, cancellationToken);
        return NoContent();
    }

    [HttpGet("google/credentials")]
    [Authorize]
    public async Task<ActionResult<GoogleClientCredentialsSummaryDto>> GetGoogleCredentialsSummary(
        CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return ApiResult.Forbidden("Utilisateur introuvable.");

        var summary = await _connectionService.GetGoogleClientCredentialsSummaryAsync(localUserId.Value, cancellationToken);
        return Ok(summary);
    }

    [HttpDelete("google/credentials")]
    [Authorize]
    public async Task<IActionResult> DeleteGoogleCredentials(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return ApiResult.Forbidden("Utilisateur introuvable.");

        await _connectionService.DeleteGoogleClientCredentialsAsync(localUserId.Value, cancellationToken);
        return NoContent();
    }
    // GET /api/email-connections/google/login ───────────────────────────────────

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
            return ApiResult.Forbidden("Utilisateur authentifié introuvable. Veuillez compléter l'onboarding.");

        var loginUrl = await _connectionService.GetGoogleLoginUrlAsync(localUserId.Value, cancellationToken);
        return Redirect(loginUrl);
    }
    // GET /api/email-connections/google/login-url ────────────────────────────────

    /// <summary>
    /// Initiates the Gmail OAuth flow for the current authenticated user.
    /// Returns the Google authorization page URL to be navigated to by the frontend.
    /// </summary>
    [HttpGet("google/login-url")]
    [Authorize]
    public async Task<ActionResult<GoogleLoginUrlDto>> GetGoogleLoginUrl(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return ApiResult.Forbidden("Utilisateur authentifié introuvable. Veuillez compléter l'onboarding.");

        var loginUrl = await _connectionService.GetGoogleLoginUrlAsync(localUserId.Value, cancellationToken);
        return Ok(new GoogleLoginUrlDto(loginUrl));
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
            return ApiResult.BadRequest("Paramètres 'code' ou 'state' manquants.");
        }

        try
        {
            await _connectionService.HandleGoogleCallbackAsync(code, state, cancellationToken);

            return Redirect(BuildFrontendOAuthRedirectUrl(success: true));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "EmailConnections — OAuth callback failed");
            return Redirect(BuildFrontendOAuthRedirectUrl(success: false, error: ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailConnections — unexpected error in OAuth callback");
            return Redirect(BuildFrontendOAuthRedirectUrl(
                success: false,
                error: "An unexpected error occurred during Gmail connection."));
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
            return ApiResult.Forbidden("Utilisateur introuvable.");

        var status = await _connectionService.GetStatusAsync(localUserId.Value, cancellationToken);
        return Ok(status);
    }

    // ── DELETE /api/email-connections ─────────────────────────────────────────────
    
    /// <summary>
    /// Disconnects the Gmail account for the authenticated user.
    /// </summary>
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return ApiResult.Forbidden("Utilisateur introuvable.");

        await _connectionService.DisconnectAsync(localUserId.Value, cancellationToken);
        return NoContent();
    }

    // ── POST /api/email-connections/verify ────────────────────────────────────────

    /// <summary>
    /// Deep-verifies the Gmail connection by trying to refresh tokens.
    /// </summary>
    [HttpPost("verify")]
    [Authorize]
    public async Task<ActionResult<EmailConnectionStatusDto>> Verify(CancellationToken cancellationToken)
    {
        var localUserId = await ResolveLocalUserIdAsync();
        if (localUserId is null)
            return ApiResult.Forbidden("Utilisateur introuvable.");

        var status = await _connectionService.VerifyConnectionAsync(localUserId.Value, cancellationToken);
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

        var userId = await _profile.FindUserIdAsync(keycloakId);

        if (userId == null)
        {
            _logger.LogWarning("EmailConnections — User with Keycloak ID {KeycloakId} not found in local database.", keycloakId);
        }

        return userId;
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

