using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Modules.Messaging.Domain;

namespace NextStep.Modules.Messaging.Infrastructure.Repositories;

public class UserEmailConnectionRepository : IUserEmailConnectionRepository
{
    private readonly MessagingDbContext _db;

    public UserEmailConnectionRepository(MessagingDbContext db)
    {
        _db = db;
    }

    public async Task<UserEmailConnection?> GetByUserAndProviderAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserEmailConnections
            .FirstOrDefaultAsync(
                c => c.UserId == userId && c.Provider == provider,
                cancellationToken);
    }

    public async Task UpsertAsync(
        UserEmailConnection connection,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.UserEmailConnections
            .FirstOrDefaultAsync(
                c => c.UserId == connection.UserId && c.Provider == connection.Provider,
                cancellationToken);

        if (existing is null)
        {
            _db.UserEmailConnections.Add(connection);
        }
        else
        {
            existing.EmailAddress             = connection.EmailAddress;
            existing.AccessTokenEncrypted     = connection.AccessTokenEncrypted;
            existing.RefreshTokenEncrypted    = connection.RefreshTokenEncrypted;
            existing.AccessTokenExpiresAtUtc  = connection.AccessTokenExpiresAtUtc;
            existing.UpdatedAtUtc             = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.UserEmailConnections
            .FirstOrDefaultAsync(
                c => c.UserId == userId && c.Provider == provider,
                cancellationToken);

        if (existing != null)
        {
            _db.UserEmailConnections.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
