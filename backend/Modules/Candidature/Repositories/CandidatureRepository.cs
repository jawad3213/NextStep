using Microsoft.EntityFrameworkCore;
using NextStep.data;
using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;

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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
