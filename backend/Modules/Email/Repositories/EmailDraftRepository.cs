using backend.data;
using backend.Modules.Email.Models;

namespace backend.Modules.Email.Repositories;

public class EmailDraftRepository : IEmailDraftRepository
{
    private readonly AppDbContext _db;

    public EmailDraftRepository(AppDbContext db)
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

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}