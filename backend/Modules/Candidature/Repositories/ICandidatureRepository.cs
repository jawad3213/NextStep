using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;
using NextStep.Modules.Candidature.Models;

namespace NextStep.Modules.Candidature.Repositories;

public interface ICandidatureRepository
{
    Task<CandidatureEntity> AddAsync(
        CandidatureEntity candidature,
        CancellationToken cancellationToken = default);

    Task<CandidatureEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<CandidatureEntity>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(List<CandidatureEntity> Items, int Total)> GetByUserIdPagedAsync(
        Guid userId,
        int offset,
        int limit,
        bool interviewOnly = false,
        CancellationToken cancellationToken = default);

    Task<CandidatureEntity?> GetByUserAndOfferAsync(
        Guid userId,
        Guid offerId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        CandidatureEntity candidature,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        CandidatureEntity candidature,
        CancellationToken cancellationToken = default);

    // ── Notes ───────────────────────────────────────────────────────────────────
    Task<List<CandidatureNote>> GetNotesAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);

    Task<CandidatureNote> AddNoteAsync(
        CandidatureNote note,
        CancellationToken cancellationToken = default);

    // ── Status History ──────────────────────────────────────────────────────────
    Task<List<CandidatureStatusHistory>> GetHistoryAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);

    Task<CandidatureStatusHistory> AddHistoryAsync(
        CandidatureStatusHistory history,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
