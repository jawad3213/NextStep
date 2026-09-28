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
public class IdentityController(IUserService userService) : ControllerBase
{
    /// <summary>Synchronise un utilisateur Keycloak dans la base locale.</summary>
    [HttpPost("sync")]
    public async Task<ActionResult<MessageResponse>> SyncUser([FromBody] UserSyncDto payload)
    {
        await userService.SyncUserFromKeycloakAsync(payload);
        return Ok(new MessageResponse("Synchronisation réussie."));
    }

    /// <summary>Récupère le profil utilisateur courant (JIT provisioning).</summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserProfileResponse>> GetUserProfile()
    {
        var user = await userService.EnsureUserCreatedAsync(User);
        return Ok(new UserProfileResponse(
            "Profil récupéré avec succès (Synchronisé avec Keycloak).",
            UserProfileDto.From(user)));
    }

    /// <summary>Retourne le statut d'onboarding (complété ou non) et le score du profil.</summary>
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

    /// <summary>Met à jour les informations d'onboarding "soft" (objectif, niveau, secteur).</summary>
    [HttpPost("soft-onboarding")]
    [Authorize]
    public async Task<ActionResult<SoftOnboardingResponse>> UpdateSoftOnboarding([FromBody] SoftOnboardingDto dto)
    {
        var user = await userService.UpdateSoftOnboardingAsync(GetKeycloakId(), dto);
        return Ok(new SoftOnboardingResponse(
            "Onboarding soft complété.",
            new SoftOnboardingData(user.OnboardingCompleted)));
    }

    /// <summary>Retourne le statut de complétion du profil pour le dashboard (inclut les sections manquantes).</summary>
    [HttpGet("profile-status")]
    [Authorize]
    public async Task<ActionResult<ProfileStatusDto>> GetProfileStatus() =>
        Ok(await userService.GetProfileStatusAsync(GetKeycloakId()));

    /// <summary>Extracts the Keycloak ID (sub claim) from the authenticated user.</summary>
    private string GetKeycloakId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedException("Impossible d'extraire l'identifiant Keycloak.");
}
