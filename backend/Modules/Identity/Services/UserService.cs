using System.Security.Claims;
using backend.Modules.Identity.DTOs;
using backend.Modules.Identity.Models;
using backend.Modules.Identity.Repositories;

namespace backend.Modules.Identity.Services
{
    public interface IUserService
    {
        Task SyncUserFromKeycloakAsync(UserSyncDto syncDto);
        Task<UserEntity> EnsureUserCreatedAsync(ClaimsPrincipal userPrincipal);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository userRepository, ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task SyncUserFromKeycloakAsync(UserSyncDto syncDto)
        {
            var existingUser = await _userRepository.GetByKeycloakIdAsync(syncDto.KeycloakId);
            
            if (existingUser != null)
            {
                _logger.LogInformation("Utilisateur {KeycloakId} existe déjà dans la base locale.", syncDto.KeycloakId);
                return;
            }

            var newUser = new UserEntity
            {
                KeycloakId = syncDto.KeycloakId,
                Email = syncDto.Email,
                Nom = syncDto.LastName,
                Prenom = syncDto.FirstName,
                DateInscription = DateTime.UtcNow
            };

            await _userRepository.CreateUserAsync(newUser);
            _logger.LogInformation("Utilisateur {KeycloakId} créé avec succès via synchronisation.", syncDto.KeycloakId);
        }

        public async Task<UserEntity> EnsureUserCreatedAsync(ClaimsPrincipal userPrincipal)
        {
            // Extraire le Keycloak ID (généralement le claim "sub" ou configuré via NameClaimType)
            var keycloakId = userPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? userPrincipal.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(keycloakId))
            {
                throw new UnauthorizedAccessException("Impossible d'extraire l'identifiant Keycloak du token.");
            }

            var existingUser = await _userRepository.GetByKeycloakIdAsync(keycloakId);
            if (existingUser != null) return existingUser;

            // Si l'utilisateur n'existe pas, on le crée à partir des infos du token (JIT Provisioning)
            var newUser = new UserEntity
            {
                KeycloakId = keycloakId,
                Email = userPrincipal.FindFirst(ClaimTypes.Email)?.Value ?? 
                        userPrincipal.FindFirst("email")?.Value ?? "unknown@email.com",
                Prenom = userPrincipal.FindFirst(ClaimTypes.GivenName)?.Value ?? 
                         userPrincipal.FindFirst("given_name")?.Value,
                Nom = userPrincipal.FindFirst(ClaimTypes.Surname)?.Value ?? 
                      userPrincipal.FindFirst("family_name")?.Value,
                DateInscription = DateTime.UtcNow
            };

            await _userRepository.CreateUserAsync(newUser);
            _logger.LogInformation("Synchronisation JIT : Nouvel utilisateur créé pour KeycloakId {KeycloakId}", keycloakId);
            
            return newUser;
        }
    }
}
