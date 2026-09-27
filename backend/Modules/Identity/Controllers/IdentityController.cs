using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Identity.DTOs;
using NextStep.Modules.Identity.Services;

namespace NextStep.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/identity")]
    public class IdentityController : ControllerBase
    {
        private readonly IUserService _userService;

        public IdentityController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Synchronise un utilisateur Keycloak dans la base locale.
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> SyncUser([FromBody] UserSyncDto payload)
        {
            if (string.IsNullOrEmpty(payload.KeycloakId) || string.IsNullOrEmpty(payload.Email))
            {
                return BadRequest(new { error = "Payload invalide : KeycloakId et Email sont requis." });
            }

            await _userService.SyncUserFromKeycloakAsync(payload);
            return Ok(new { message = "Synchronisation réussie." });
        }

        /// <summary>
        /// Récupère le profil utilisateur courant (JIT provisioning).
        /// </summary>
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetUserProfile()
        {
            var userProfile = await _userService.EnsureUserCreatedAsync(User);
            return Ok(new 
            { 
                message = "Profil récupéré avec succès (Synchronisé avec Keycloak).",
                data = userProfile 
            });
        }

        /// <summary>
        /// Retourne le statut d'onboarding (complété ou non) et le score du profil.
        /// </summary>
        [HttpGet("onboarding-status")]
        [Authorize]
        public async Task<IActionResult> GetOnboardingStatus()
        {
            var keycloakId = GetKeycloakId();
            var status = await _userService.GetProfileStatusAsync(keycloakId);
            return Ok(new { 
                onboardingCompleted = status.OnboardingCompleted,
                profileCompleted = status.ProfileCompleted,
                profileScore = status.ProfileScore,
                completionPercent = status.CompletionPercent
            });
        }

        /// <summary>
        /// Validates the profile server-side (same 85% rule as the stepper) and unlocks the app.
        /// </summary>
        [HttpPost("complete-profile")]
        [Authorize]
        public async Task<IActionResult> CompleteProfile()
        {
            var result = await _userService.CompleteProfileAsync(GetKeycloakId());
            if (result.Succeeded) return Ok(result);
            // Standard error contract ({ error, ... }) read by the frontend's extractApiError.
            return BadRequest(new { error = result.Message, result.CompletionPercent, result.RequiredPercent });
        }

        /// <summary>
        /// Met à jour les informations d'onboarding "soft" (objectif, niveau, secteur).
        /// </summary>
        [HttpPost("soft-onboarding")]
        [Authorize]
        public async Task<IActionResult> UpdateSoftOnboarding([FromBody] SoftOnboardingDto dto)
        {
            var keycloakId = GetKeycloakId();
            var user = await _userService.UpdateSoftOnboardingAsync(keycloakId, dto);
            return Ok(new
            {
                message = "Onboarding soft complété.",
                data = new { user.OnboardingCompleted }
            });
        }

        /// <summary>
        /// Retourne le statut de complétion du profil pour le dashboard (inclut les sections manquantes).
        /// </summary>
        [HttpGet("profile-status")]
        [Authorize]
        public async Task<IActionResult> GetProfileStatus()
        {
            var keycloakId = GetKeycloakId();
            var status = await _userService.GetProfileStatusAsync(keycloakId);
            return Ok(status);
        }

        /// <summary>
        /// Extracts the Keycloak ID (sub claim) from the authenticated user.
        /// </summary>
        private string GetKeycloakId()
        {
            var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(keycloakId))
                throw new UnauthorizedAccessException("Impossible d'extraire l'identifiant Keycloak.");

            return keycloakId;
        }
    }
}
