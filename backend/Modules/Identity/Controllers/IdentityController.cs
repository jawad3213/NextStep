using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.Modules.Identity.DTOs;
using backend.Modules.Identity.Services;

namespace backend.Modules.Identity.Controllers
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
                return BadRequest("Payload invalide : KeycloakId et Email sont requis.");
            }

            try
            {
                await _userService.SyncUserFromKeycloakAsync(payload);
                return Ok(new { message = "Synchronisation réussie." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur interne : {ex.Message}");
            }
        }

        /// <summary>
        /// Récupère le profil utilisateur courant (JIT provisioning).
        /// </summary>
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetUserProfile()
        {
            try
            {
                var userProfile = await _userService.EnsureUserCreatedAsync(User);
                return Ok(new 
                { 
                    message = "Profil récupéré avec succès (Synchronisé avec Keycloak).",
                    data = userProfile 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la synchronisation du profil : {ex.Message}");
            }
        }

        /// <summary>
        /// Retourne le statut d'onboarding (complété ou non) et le score du profil.
        /// </summary>
        [HttpGet("onboarding-status")]
        [Authorize]
        public async Task<IActionResult> GetOnboardingStatus()
        {
            try
            {
                var keycloakId = GetKeycloakId();
                var status = await _userService.GetProfileStatusAsync(keycloakId);
                return Ok(new { 
                    onboardingCompleted = status.OnboardingCompleted,
                    profileScore = status.ProfileScore
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Utilisateur non trouvé.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur : {ex.Message}");
            }
        }

        /// <summary>
        /// Met à jour les informations d'onboarding "soft" (objectif, niveau, secteur).
        /// </summary>
        [HttpPost("soft-onboarding")]
        [Authorize]
        public async Task<IActionResult> UpdateSoftOnboarding([FromBody] SoftOnboardingDto dto)
        {
            try
            {
                var keycloakId = GetKeycloakId();
                var user = await _userService.UpdateSoftOnboardingAsync(keycloakId, dto);
                return Ok(new
                {
                    message = "Onboarding soft complété.",
                    data = new { user.OnboardingCompleted }
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Utilisateur non trouvé.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur : {ex.Message}");
            }
        }

        /// <summary>
        /// Retourne le statut de complétion du profil pour le dashboard (inclut les sections manquantes).
        /// </summary>
        [HttpGet("profile-status")]
        [Authorize]
        public async Task<IActionResult> GetProfileStatus()
        {
            try
            {
                var keycloakId = GetKeycloakId();
                var status = await _userService.GetProfileStatusAsync(keycloakId);
                return Ok(status);
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Utilisateur non trouvé.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur : {ex.Message}");
            }
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
