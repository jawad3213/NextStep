// ============================================================
// Modules/Offer/Repositories/OfferRepository.cs
// Accès base de données pour les offres d'emploi
// ============================================================
using Microsoft.EntityFrameworkCore;
using NextStep.data;
using NextStep.Modules.Offer.Models;

namespace NextStep.Modules.Offer.Repositories;

public interface IOfferRepository
{
    Task<OffreEmploi> SaveAsync(OffreEmploi offre, CancellationToken ct = default);
    Task<OffreEmploi?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OffreEmploi?> GetByIdWithAnalysisAsync(Guid id, CancellationToken ct = default);
    Task UpdateAnalyseJsonAsync(Guid id, string analyseJson, CancellationToken ct = default);
    Task<List<OffreEmploi>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}

public class OfferRepository(AppDbContext db) : IOfferRepository
{
    public async Task<OffreEmploi> SaveAsync(OffreEmploi offre, CancellationToken ct = default)
    {
        db.OffresEmploi.Add(offre);
        await db.SaveChangesAsync(ct);
        return offre;
    }

    public async Task<OffreEmploi?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.OffresEmploi.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<OffreEmploi?> GetByIdWithAnalysisAsync(Guid id, CancellationToken ct = default)
        => await db.OffresEmploi.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task UpdateAnalyseJsonAsync(Guid id, string analyseJson, CancellationToken ct = default)
    {
        var offre = await db.OffresEmploi.FindAsync([id], ct);
        if (offre is null) return;
        offre.AnalyseJson = analyseJson;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<OffreEmploi>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.OffresEmploi
            .AsNoTracking()
            .Where(o => o.UtilisateurId == userId)
            .OrderByDescending(o => o.DateCreation)
            .ToListAsync(ct);
}
