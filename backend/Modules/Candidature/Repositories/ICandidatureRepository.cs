using CandidatureEntity = backend.Modules.Candidature.Models.Candidature;

namespace backend.Modules.Candidature.Repositories;

public interface ICandidatureRepository
{
    Task<CandidatureEntity> AddAsync(
        CandidatureEntity candidature,
        CancellationToken cancellationToken = default);

    Task<CandidatureEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}