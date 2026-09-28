using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Repositories;

public interface IUserOAuthCredentialRepository
{
    Task<UserOAuthCredential?> GetByUserAndProviderAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        UserOAuthCredential credential,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default);
}
