using Microsoft.EntityFrameworkCore;
using NextStep.data;
using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Repositories;

public class UserOAuthCredentialRepository : IUserOAuthCredentialRepository
{
    private readonly AppDbContext _db;

    public UserOAuthCredentialRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UserOAuthCredential?> GetByUserAndProviderAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserOAuthCredentials
            .FirstOrDefaultAsync(
                c => c.UserId == userId && c.Provider == provider,
                cancellationToken);
    }

    public async Task UpsertAsync(
        UserOAuthCredential credential,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.UserOAuthCredentials
            .FirstOrDefaultAsync(
                c => c.UserId == credential.UserId && c.Provider == credential.Provider,
                cancellationToken);

        if (existing is null)
        {
            _db.UserOAuthCredentials.Add(credential);
        }
        else
        {
            existing.ClientIdEncrypted     = credential.ClientIdEncrypted;
            existing.ClientSecretEncrypted = credential.ClientSecretEncrypted;
            existing.RedirectUriOverride   = credential.RedirectUriOverride;
            existing.UpdatedAtUtc          = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.UserOAuthCredentials
            .FirstOrDefaultAsync(
                c => c.UserId == userId && c.Provider == provider,
                cancellationToken);

        if (existing != null)
        {
            _db.UserOAuthCredentials.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
