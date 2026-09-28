using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Modules.Messaging.Domain;

namespace NextStep.Modules.Messaging.Infrastructure.Repositories;

public class EmailDraftRepository : IEmailDraftRepository
{
    private readonly MessagingDbContext _db;

    public EmailDraftRepository(MessagingDbContext db)
    {
        _db = db;
    }

    public async Task<EmailDraft> AddAsync(
        EmailDraft draft,
        CancellationToken cancellationToken = default)
    {
        _db.EmailDrafts.Add(draft);
        await _db.SaveChangesAsync(cancellationToken);
        return draft;
    }

    public async Task<EmailDraft?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _db.EmailDrafts
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<EmailDraft>> GetByCandidatureIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        return await _db.EmailDrafts
            .AsNoTracking()
            .Where(x => x.CandidatureId == candidatureId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<EmailDraft>> GetPendingReplyCheckAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.EmailDrafts
            .Where(d =>
                d.IsSent &&
                d.ProviderThreadId != null &&
                d.SentAtUtc != null)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
