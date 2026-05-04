using Microsoft.EntityFrameworkCore;
using NextStep.data;
using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Repositories;

public class UserEmailConnectionRepository : IUserEmailConnectionRepository
{
    private readonly AppDbContext _db;

    public UserEmailConnectionRepository(AppDbContext db)
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
}
