using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Shared.Pagination;

namespace NextStep.Modules.Applications.Application.Services;

public interface ICandidatureService
{
    /// <param name="source">Who made the change, recorded in the status history ("user", "ai_sn").</param>
    Task<CandidatureDto> CreateAsync(
        Guid userId,
        CreateCandidatureDto dto,
        string source = "user",
        CancellationToken cancellationToken = default);

    Task<CandidatureDto?> GetByIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The user's application; throws <c>NotFoundException</c> when it does not exist and
    /// <c>ForbiddenException</c> when it belongs to another user.
    /// </summary>
    Task<CandidatureDto> GetOwnedAsync(
        Guid userId,
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

    /// <param name="source">Who made the change, recorded in the status history ("user", "ai_sn").</param>
    Task<CandidatureDto?> UpdateStatutAsync(
        Guid candidatureId,
        UpdateStatutDto dto,
        string source = "user",
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
