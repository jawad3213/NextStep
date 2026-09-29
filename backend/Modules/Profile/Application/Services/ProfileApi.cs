using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Profile.Contracts;
using NextStep.Modules.Profile.Infrastructure.Persistence;
using NextStep.Modules.Profile.Infrastructure.Repositories;

namespace NextStep.Modules.Profile.Application.Services;

public class ProfileApi(
    IUserService userService,
    IUserRepository userRepository,
    ProfileDbContext db,
    ILogger<ProfileApi> logger) : IProfileApi
{
    public async Task<Guid> EnsureUserIdAsync(ClaimsPrincipal principal)
    {
        var user = await userService.EnsureUserCreatedAsync(principal);
        return user.Id;
    }

    public async Task<Guid?> TryResolveUserIdAsync(ClaimsPrincipal principal)
    {
        try
        {
            return await EnsureUserIdAsync(principal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnsureUserCreatedAsync failed, falling back to direct Keycloak lookup");
            var keycloakId = principal.FindFirstValue("sub")
                          ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirstValue("uid");

            if (string.IsNullOrWhiteSpace(keycloakId))
                return null;

            return (await userRepository.GetByKeycloakIdAsync(keycloakId))?.Id;
        }
    }

    public async Task<Guid?> FindUserIdAsync(string keycloakIdOrUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keycloakIdOrUserId))
            return null;

        var value = keycloakIdOrUserId.Trim();
        if (Guid.TryParse(value, out var userId) && await db.Utilisateurs.AnyAsync(u => u.Id == userId, ct))
            return userId;

        var lowered = value.ToLower();
        return await db.Utilisateurs
            .Where(u => u.KeycloakId.ToLower() == lowered)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UserIdentity?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Utilisateurs
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserIdentity(u.Id, u.Prenom, u.Nom, u.Email))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CandidateProfile?> GetCandidateProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Utilisateurs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return null;

        var skills = await db.Competences
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.Nom ?? "")
            .Where(n => n.Length > 0)
            .ToListAsync(ct);

        var experiences = await db.Experiences
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => new ExperienceSummary(e.Poste, e.Entreprise, e.DateDebut, e.DateFin))
            .ToListAsync(ct);

        var education = await db.Formations
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => new EducationSummary(f.Diplome, f.Etablissement, f.Annee))
            .ToListAsync(ct);

        var projects = await db.Projets
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.TitreProjet ?? "")
            .Where(t => t.Length > 0)
            .ToListAsync(ct);

        var certifications = await db.Certifications
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.Titre ?? "")
            .Where(t => t.Length > 0)
            .ToListAsync(ct);

        return new CandidateProfile(
            new UserIdentity(user.Id, user.Prenom, user.Nom, user.Email),
            user.Telephone,
            user.TitrePoste,
            skills,
            experiences,
            education,
            projects,
            certifications);
    }
}
