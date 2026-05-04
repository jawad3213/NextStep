using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Repositories;

public interface IOAuthStateRepository
{
    Task AddAsync(OAuthState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a valid (unused, not expired) OAuthState by provider and state token hash.
    /// </summary>
    Task<OAuthState?> FindValidAsync(
        string provider,
        string stateTokenHash,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
