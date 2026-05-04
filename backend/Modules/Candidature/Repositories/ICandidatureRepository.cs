using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;

namespace NextStep.Modules.Candidature.Repositories;

public interface ICandidatureRepository
{
    Task<CandidatureEntity> AddAsync(
        CandidatureEntity candidature,
        CancellationToken cancellationToken = default);

    Task<CandidatureEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CandidatureEntity?> GetByUserAndOfferAsync(
        Guid userId,
        Guid offerId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}