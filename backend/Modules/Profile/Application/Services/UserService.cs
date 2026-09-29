using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using NextStep.Modules.Profile.Application.Dtos;
using NextStep.Modules.Profile.Domain;
using NextStep.Modules.Profile.Infrastructure.Repositories;
using NextStep.Shared.ErrorHandling;
using NextStep.Modules.Profile.Infrastructure.Persistence;

namespace NextStep.Modules.Profile.Application.Services {
    public interface IUserService
    {
        Task<UserEntity> EnsureUserCreatedAsync(ClaimsPrincipal userPrincipal);
        Task<UserEntity> UpdateSoftOnboardingAsync(string keycloakId, SoftOnboardingDto dto);
        Task<ProfileStatusDto> GetProfileStatusAsync(string keycloakId);
        Task<CompleteProfileResult> CompleteProfileAsync(string keycloakId);
        Task UpdateProfileScoreAsync(Guid userId);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ProfileDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository userRepository, ProfileDbContext context, ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// The local user of the signed-in Keycloak account, created on first sight from the
        /// token's claims (the only way users are created).
        /// </summary>
        public async Task<UserEntity> EnsureUserCreatedAsync(ClaimsPrincipal userPrincipal)
        {
            var keycloakId = userPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? userPrincipal.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(keycloakId))
            {
                throw new UnauthorizedException("Unable to extract Keycloak identifier from token.");
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
            if (user == null) throw new NotFoundException("User not found.");

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
            if (user == null) throw new NotFoundException("User not found.");

            var missingSections = new List<string>();
            
            bool hasExp = await _context.Experiences.AnyAsync(e => e.UserId == user.Id);
            bool hasSkills = await _context.Competences.AnyAsync(c => c.UserId == user.Id);
            bool hasProjects = await _context.Projets.AnyAsync(p => p.UserId == user.Id);
            bool hasEdu = await _context.Formations.AnyAsync(f => f.UserId == user.Id);

            if (!hasExp) missingSections.Add("Experiences");
            if (!hasSkills) missingSections.Add("Skills");
            if (!hasProjects) missingSections.Add("Projects");
            if (!hasEdu) missingSections.Add("Education");

            int score = await ComputeProfileScoreInternalAsync(user);
            user.ProfileScore = score;
            await _userRepository.UpdateUserAsync(user);

            return new ProfileStatusDto
            {
                IsComplete = score >= 70,
                OnboardingCompleted = user.OnboardingCompleted,
                ProfileCompleted = user.ProfileCompleted,
                ProfileScore = score,
                CompletionPercent = await ComputeCompletionPercentAsync(user),
                MissingSections = missingSections
            };
        }

        /// <summary>Minimum profile completion required to unlock the application.</summary>
        public const int RequiredCompletionPercent = 85;

        /// <summary>
        /// Validates the profile on the server and unlocks the application for this user.
        /// Called by the profile stepper's "Finish" button; the frontend check alone is not trusted.
        /// </summary>
        public async Task<CompleteProfileResult> CompleteProfileAsync(string keycloakId)
        {
            var user = await _userRepository.GetByKeycloakIdAsync(keycloakId);
            if (user == null) throw new NotFoundException("User not found.");

            var percent = await ComputeCompletionPercentAsync(user);
            var result = new CompleteProfileResult { CompletionPercent = percent, RequiredPercent = RequiredCompletionPercent };

            if (!user.OnboardingCompleted)
            {
                result.Message = "Complete the onboarding questions first.";
                return result;
            }
            if (percent < RequiredCompletionPercent)
            {
                result.Message = $"Complete at least {RequiredCompletionPercent}% of your profile to continue (currently {percent}%).";
                return result;
            }

            if (!user.ProfileCompleted)
            {
                user.ProfileCompleted = true;
                await _userRepository.UpdateUserAsync(user);
            }
            result.Succeeded = true;
            result.Message = "Profile completed.";
            return result;
        }

        /// <summary>
        /// Profile completion (0-100). Same weights as completionPercentage in
        /// frontend/src/app/features/profile/profile.service.ts: keep both in sync.
        /// </summary>
        private async Task<int> ComputeCompletionPercentAsync(UserEntity user)
        {
            int score = 0;
            // Contact info (12)
            if (!string.IsNullOrWhiteSpace(user.Prenom)) score += 2;
            if (!string.IsNullOrWhiteSpace(user.Nom)) score += 2;
            if (!string.IsNullOrWhiteSpace(user.Email)) score += 2;
            if (!string.IsNullOrWhiteSpace(user.Telephone)) score += 2;
            if (!string.IsNullOrWhiteSpace(user.TitrePoste)) score += 2;
            if (!string.IsNullOrWhiteSpace(user.Ville)) score += 1;
            if (!string.IsNullOrWhiteSpace(user.Pays)) score += 1;

            if (await _context.Formations.AnyAsync(f => f.UserId == user.Id)) score += 14;
            if (await _context.Experiences.AnyAsync(e => e.UserId == user.Id)) score += 14;

            // Skills (16) and languages (4) share the competence table; languages have a "lang..." type.
            var competenceTypes = await _context.Competences
                .Where(c => c.UserId == user.Id)
                .Select(c => c.TypeCompetence ?? string.Empty)
                .ToListAsync();
            static bool IsLanguage(string type) =>
                type.Contains("lang", StringComparison.OrdinalIgnoreCase) || type.Contains("linguist", StringComparison.OrdinalIgnoreCase);
            if (competenceTypes.Any(t => !IsLanguage(t))) score += 16;
            if (competenceTypes.Any(IsLanguage)) score += 4;

            if (await _context.Projets.AnyAsync(p => p.UserId == user.Id)) score += 14;
            if ((user.ResumeProfessionnel ?? string.Empty).Length > 50) score += 14;
            if (await _context.Certifications.AnyAsync(c => c.UserId == user.Id)) score += 12;

            return Math.Min(score, 100);
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
