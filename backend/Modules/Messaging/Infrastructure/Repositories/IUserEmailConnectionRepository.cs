using NextStep.Modules.Messaging.Domain;

namespace NextStep.Modules.Messaging.Infrastructure.Repositories;

public interface IUserEmailConnectionRepository
{
    Task<UserEmailConnection?> GetByUserAndProviderAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        UserEmailConnection connection,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default);
}
