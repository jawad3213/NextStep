using System.Security.Claims;

namespace NextStep.Modules.Profile.Contracts;

/// <summary>
/// Public contract of the Profile module: the only way other modules resolve the
/// current user or read candidate data. Profile tables are never read directly.
/// </summary>
public interface IProfileApi
{
    /// <summary>Returns the local user id of the authenticated principal, creating the user on first sight.</summary>
    Task<Guid> EnsureUserIdAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Like <see cref="EnsureUserIdAsync"/>, but never throws: falls back to a plain lookup
    /// of the token subject and returns null when the user cannot be resolved.
    /// </summary>
    Task<Guid?> TryResolveUserIdAsync(ClaimsPrincipal principal);

    /// <summary>Local user id for a Keycloak subject or a local id string; null when unknown. Never creates.</summary>
    Task<Guid?> FindUserIdAsync(string keycloakIdOrUserId, CancellationToken ct = default);

    Task<UserIdentity?> GetUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Candidate data used to write applications (emails, letters).</summary>
    Task<CandidateProfile?> GetCandidateProfileAsync(Guid userId, CancellationToken ct = default);
}

public sealed record UserIdentity(Guid UserId, string? FirstName, string? LastName, string? Email)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed record CandidateProfile(
    UserIdentity User,
    string? Phone,
    string? CurrentTitle,
    IReadOnlyList<string> Skills,
    IReadOnlyList<ExperienceSummary> Experiences,
    IReadOnlyList<EducationSummary> Education,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Certifications);

public sealed record ExperienceSummary(string? Title, string? Company, DateTime? StartDate, DateTime? EndDate);

public sealed record EducationSummary(string? Degree, string? School, int Year);
