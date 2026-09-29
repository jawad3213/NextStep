using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Profile.Application.Dtos;
using NextStep.Modules.Profile.Application.Services;
using NextStep.Shared.Api;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Profile.Api;

[ApiController]
[Route("api/identity")]
[Authorize]
public class IdentityController(IUserService userService) : ControllerBase
{
    /// <summary>Retrieves the current user profile (JIT provisioning).</summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserProfileResponse>> GetUserProfile()
    {
        var user = await userService.EnsureUserCreatedAsync(User);
        return Ok(new UserProfileResponse(
            "User profile retrieved successfully (synchronized with Keycloak).",
            UserProfileDto.From(user)));
    }

    /// <summary>Returns the onboarding status and profile score.</summary>
    [HttpGet("onboarding-status")]
    [Authorize]
    public async Task<ActionResult<OnboardingStatusResponse>> GetOnboardingStatus()
    {
        var status = await userService.GetProfileStatusAsync(GetKeycloakId());
        return Ok(new OnboardingStatusResponse(
            status.OnboardingCompleted,
            status.ProfileCompleted,
            status.ProfileScore,
            status.CompletionPercent));
    }

    /// <summary>Validates the profile server-side (same 85% rule as the stepper) and unlocks the app.</summary>
    [HttpPost("complete-profile")]
    [Authorize]
    public async Task<ActionResult<CompleteProfileResult>> CompleteProfile()
    {
        var result = await userService.CompleteProfileAsync(GetKeycloakId());
        if (result.Succeeded) return Ok(result);
        // Standard error contract ({ error, ... }) read by the frontend's extractApiError.
        return BadRequest(new CompleteProfileErrorResponse(result.Message, result.CompletionPercent, result.RequiredPercent));
    }

    /// <summary>Updates soft onboarding information (objective, level, sector).</summary>
    [HttpPost("soft-onboarding")]
    [Authorize]
    public async Task<ActionResult<SoftOnboardingResponse>> UpdateSoftOnboarding([FromBody] SoftOnboardingDto dto)
    {
        var user = await userService.UpdateSoftOnboardingAsync(GetKeycloakId(), dto);
        return Ok(new SoftOnboardingResponse(
            "Soft onboarding completed successfully.",
            new SoftOnboardingData(user.OnboardingCompleted)));
    }

    /// <summary>Returns the profile completion status for the dashboard (includes missing sections).</summary>
    [HttpGet("profile-status")]
    [Authorize]
    public async Task<ActionResult<ProfileStatusDto>> GetProfileStatus() =>
        Ok(await userService.GetProfileStatusAsync(GetKeycloakId()));

    /// <summary>Extracts the Keycloak ID (sub claim) from the authenticated user.</summary>
    private string GetKeycloakId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedException("Unable to extract Keycloak identifier.");
}
