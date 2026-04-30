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