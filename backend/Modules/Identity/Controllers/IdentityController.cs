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
    }
}

