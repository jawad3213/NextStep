using Microsoft.EntityFrameworkCore;
using NextStep.data;
using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Repositories;

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
            .Include(d => d.Candidature)               // tracked — job will update candidature
            .Where(d =>
                d.IsSent &&
                d.ProviderThreadId != null &&
                d.SentAtUtc != null &&
                d.Candidature != null &&
                (
                    !d.Candidature.HasResponse ||
                    (
                        d.Candidature.HasResponse &&
                        d.Candidature.ResponseStatus == "REPONSE_RECUE" &&
                        (d.Candidature.ResponseConfidence == null || d.Candidature.ResponseConfidence <= 0.01)
                    )
                ))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
