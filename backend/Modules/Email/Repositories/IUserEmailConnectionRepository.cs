using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Repositories;

public interface IUserEmailConnectionRepository
{
    Task<UserEmailConnection?> GetByUserAndProviderAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        UserEmailConnection connection,
        CancellationToken cancellationToken = default);
}
