using Microsoft.EntityFrameworkCore;
using NextStep.data;
using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Repositories;

public class OAuthStateRepository : IOAuthStateRepository
{
    private readonly AppDbContext _db;

    public OAuthStateRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(OAuthState state, CancellationToken cancellationToken = default)
    {
        _db.OAuthStates.Add(state);
        await _db.SaveChangesAsync(cancellationToken);
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
