using backend.Modules.Candidature.DTOs;

namespace backend.Modules.Candidature.Services;

public interface ICandidatureService
{
    Task<CandidatureDto> CreateAsync(
        Guid userId,
        CreateCandidatureDto dto,
        CancellationToken cancellationToken = default);

    Task<CandidatureDto?> GetByIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);
}