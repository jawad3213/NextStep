using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using NextStep.Modules.Identity.DTOs;
using NextStep.Modules.Identity.Models;
using NextStep.Modules.Identity.Repositories;
using NextStep.data;

namespace NextStep.Modules.Identity.Services
{
    public interface IUserService
    {
        Task SyncUserFromKeycloakAsync(UserSyncDto syncDto);
        Task<UserEntity> EnsureUserCreatedAsync(ClaimsPrincipal userPrincipal);
        Task<UserEntity> UpdateSoftOnboardingAsync(string keycloakId, SoftOnboardingDto dto);
        Task<ProfileStatusDto> GetProfileStatusAsync(string keycloakId);
        Task UpdateProfileScoreAsync(Guid userId);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly AppDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository userRepository, AppDbContext context, ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _context = context;
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
                OnboardingCompleted = false,
                ProfileScore = 0,
                DateInscription = DateTime.UtcNow
            };

            await _userRepository.CreateUserAsync(newUser);
        }

        public async Task<UserEntity> EnsureUserCreatedAsync(ClaimsPrincipal userPrincipal)
        {
            var keycloakId = userPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? userPrincipal.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(keycloakId))
            {
                throw new UnauthorizedAccessException("Impossible d'extraire l'identifiant Keycloak du token.");
            }

            var existingUser = await _userRepository.GetByKeycloakIdAsync(keycloakId);
            if (existingUser != null) return existingUser;

            var newUser = new UserEntity
            {
                KeycloakId = keycloakId,
                Email = userPrincipal.FindFirst(ClaimTypes.Email)?.Value ?? 
                        userPrincipal.FindFirst("email")?.Value ?? 
                        userPrincipal.FindFirst("preferred_username")?.Value ?? "unknown@email.com",
                Prenom = userPrincipal.FindFirst(ClaimTypes.GivenName)?.Value ?? 
                         userPrincipal.FindFirst("given_name")?.Value ??
                         userPrincipal.FindFirst("name")?.Value?.Split(' ').FirstOrDefault(),
                Nom = userPrincipal.FindFirst(ClaimTypes.Surname)?.Value ?? 
                      userPrincipal.FindFirst("family_name")?.Value ??
                      userPrincipal.FindFirst("name")?.Value?.Split(' ').LastOrDefault(),
                OnboardingCompleted = false,
                ProfileScore = 0,
                DateInscription = DateTime.UtcNow
            };

            await _userRepository.CreateUserAsync(newUser);
            return newUser;
        }

        public async Task<UserEntity> UpdateSoftOnboardingAsync(string keycloakId, SoftOnboardingDto dto)
        {
            var user = await _userRepository.GetByKeycloakIdAsync(keycloakId);
            if (user == null) throw new KeyNotFoundException("Utilisateur non trouvé.");

            user.Objectif = dto.Objectif.ToString();
            user.Niveau = dto.Niveau.ToString();
            user.Secteur = dto.Secteur.ToString();
            user.OnboardingCompleted = true;

            await _userRepository.UpdateUserAsync(user);
            return user;
        }

        public async Task<ProfileStatusDto> GetProfileStatusAsync(string keycloakId)
        {
            var user = await _userRepository.GetByKeycloakIdAsync(keycloakId);
            if (user == null) throw new KeyNotFoundException("Utilisateur non trouvé.");

            var missingSections = new List<string>();
            
            bool hasExp = await _context.Experiences.AnyAsync(e => e.UserId == user.Id);
            bool hasSkills = await _context.Competences.AnyAsync(c => c.UserId == user.Id);
            bool hasProjects = await _context.Projets.AnyAsync(p => p.UserId == user.Id);
            bool hasEdu = await _context.Formations.AnyAsync(f => f.UserId == user.Id);

            if (!hasExp) missingSections.Add("Expériences");
            if (!hasSkills) missingSections.Add("Compétences");
            if (!hasProjects) missingSections.Add("Projets");
            if (!hasEdu) missingSections.Add("Formations");

            int score = await ComputeProfileScoreInternalAsync(user);
            user.ProfileScore = score;
            await _userRepository.UpdateUserAsync(user);

            return new ProfileStatusDto
            {
                IsComplete = score >= 70,
                OnboardingCompleted = user.OnboardingCompleted,
                ProfileScore = score,
                MissingSections = missingSections
            };
        }

        public async Task UpdateProfileScoreAsync(Guid userId)
        {
            var user = await _context.Utilisateurs.FindAsync(userId);
            if (user != null)
            {
                user.ProfileScore = await ComputeProfileScoreInternalAsync(user);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<int> ComputeProfileScoreInternalAsync(UserEntity user)
        {
            int score = 0;
            // Personal Info (20 pts)
            if (!string.IsNullOrWhiteSpace(user.Nom)) score += 5;
            if (!string.IsNullOrWhiteSpace(user.Prenom)) score += 5;
            if (!string.IsNullOrWhiteSpace(user.Email)) score += 5;
            if (!string.IsNullOrWhiteSpace(user.Coordonnees)) score += 5;

            // Links (10 pts)
            if (!string.IsNullOrWhiteSpace(user.LienLinkedin)) score += 5;
            if (!string.IsNullOrWhiteSpace(user.ResumeProfessionnel)) score += 5;

            // Sections (70 pts)
            if (await _context.Experiences.AnyAsync(e => e.UserId == user.Id)) score += 20;
            if (await _context.Competences.AnyAsync(c => c.UserId == user.Id)) score += 15;
            if (await _context.Projets.AnyAsync(p => p.UserId == user.Id)) score += 20;
            if (await _context.Formations.AnyAsync(f => f.UserId == user.Id)) score += 15;

            return Math.Min(score, 100);
        }
    }
}
