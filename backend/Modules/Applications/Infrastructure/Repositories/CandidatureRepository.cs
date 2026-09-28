using Microsoft.EntityFrameworkCore;
using NextStep.data;
using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;
using NextStep.Modules.Candidature.Models;

namespace NextStep.Modules.Candidature.Repositories;

public class CandidatureRepository : ICandidatureRepository
{
    private readonly AppDbContext _db;

    public CandidatureRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CandidatureEntity> AddAsync(
        CandidatureEntity candidature,
        CancellationToken cancellationToken = default)
    {
        _db.Candidatures.Add(candidature);
        await _db.SaveChangesAsync(cancellationToken);
        return candidature;
    }

    public async Task<CandidatureEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Candidatures
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdCandidature == id, cancellationToken);
    }

    public async Task<List<CandidatureEntity>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Candidatures
            .AsNoTracking()
            .Where(x => x.IdUtilisateur == userId)
            .OrderByDescending(x => x.DateCreation)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<CandidatureEntity> Items, int Total)> GetByUserIdPagedAsync(
        Guid userId,
        int offset,
        int limit,
        bool interviewOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Candidatures
            .AsNoTracking()
            .Where(x => x.IdUtilisateur == userId);

        if (interviewOnly)
        {
            query = query.Where(x =>
                (x.Statut != null && EF.Functions.Like(x.Statut, "%ENTRETIEN%")) ||
                (x.ResponseStatus != null && EF.Functions.Like(x.ResponseStatus, "%ENTRETIEN%")));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.LastResponseAtUtc ?? x.DateCreation)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<CandidatureEntity?> GetByUserAndOfferAsync(
        Guid userId,
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Candidatures
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.IdUtilisateur == userId && x.IdOffre == offerId,
                cancellationToken);
    }

    public async Task UpdateAsync(
        CandidatureEntity candidature,
        CancellationToken cancellationToken = default)
    {
        _db.Candidatures.Update(candidature);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        CandidatureEntity candidature,
        CancellationToken cancellationToken = default)
    {
        _db.Candidatures.Remove(candidature);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ── Notes ───────────────────────────────────────────────────────────────────

    public async Task<List<CandidatureNote>> GetNotesAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CandidatureNotes
            .AsNoTracking()
            .Where(n => n.CandidatureId == candidatureId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CandidatureNote> AddNoteAsync(
        CandidatureNote note,
        CancellationToken cancellationToken = default)
    {
        _db.CandidatureNotes.Add(note);
        await _db.SaveChangesAsync(cancellationToken);
        return note;
    }

    // ── Status History ──────────────────────────────────────────────────────────

    public async Task<List<CandidatureStatusHistory>> GetHistoryAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CandidatureStatusHistories
            .AsNoTracking()
            .Where(h => h.CandidatureId == candidatureId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CandidatureStatusHistory> AddHistoryAsync(
        CandidatureStatusHistory history,
        CancellationToken cancellationToken = default)
    {
        _db.CandidatureStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);
        return history;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
