using NextStep.Modules.Candidature.DTOs;

namespace NextStep.Modules.Candidature.Services;

public interface ICandidatureService
{
    Task<CandidatureDto> CreateAsync(
        Guid userId,
        CreateCandidatureDto dto,
        CancellationToken cancellationToken = default);

    Task<CandidatureDto?> GetByIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);

    Task<List<CandidatureDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}