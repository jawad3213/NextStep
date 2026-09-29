using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Modules.Messaging.Domain;

namespace NextStep.Modules.Messaging.Infrastructure.Repositories;

public class OAuthStateRepository : IOAuthStateRepository
{
    private readonly MessagingDbContext _db;

    public OAuthStateRepository(MessagingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(OAuthState state, CancellationToken cancellationToken = default)
    {
        _db.OAuthStates.Add(state);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        var expired = await _db.OAuthStates.Where(s => s.ExpiresAtUtc < olderThanUtc).ToListAsync(cancellationToken);
        if (expired.Count == 0) return 0;
        _db.OAuthStates.RemoveRange(expired);
        await _db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    public async Task<OAuthState?> FindValidAsync(
        string provider,
        string stateTokenHash,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _db.OAuthStates
            .FirstOrDefaultAsync(
                s => s.Provider == provider
                  && s.StateTokenHash == stateTokenHash
                  && !s.Used
                  && s.ExpiresAtUtc > now,
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
