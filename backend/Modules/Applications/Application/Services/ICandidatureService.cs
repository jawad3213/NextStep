using NextStep.Modules.Candidature.DTOs;
using NextStep.Shared.Pagination;

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

    Task<PagedResponse<CandidatureDto>> GetByUserIdPagedAsync(
        Guid userId,
        int offset,
        int limit,
        bool interviewOnly = false,
        CancellationToken cancellationToken = default);

    Task<CandidatureDto?> UpdateStatutAsync(
        Guid candidatureId,
        UpdateStatutDto dto,
        CancellationToken cancellationToken = default);

    Task<CandidatureDto?> UpdateAsync(
        Guid candidatureId,
        UpdateCandidatureDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);

    Task<List<CandidatureNoteDto>> GetNotesAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);

    Task<CandidatureNoteDto> AddNoteAsync(
        Guid candidatureId,
        AddNoteDto dto,
        CancellationToken cancellationToken = default);

    Task<List<CandidatureStatusHistoryDto>> GetHistoryAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);
}
