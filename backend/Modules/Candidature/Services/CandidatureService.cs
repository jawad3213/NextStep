using NextStep.Modules.Candidature.DTOs;
using NextStep.Modules.Candidature.Repositories;
using NextStep.Shared.Pagination;
using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;

namespace NextStep.Modules.Candidature.Services;

public class CandidatureService : ICandidatureService
{
    private readonly ICandidatureRepository _candidatureRepository;

    public CandidatureService(ICandidatureRepository candidatureRepository)
    {
        _candidatureRepository = candidatureRepository;
    }

    public async Task<CandidatureDto> CreateAsync(
        Guid userId,
        CreateCandidatureDto dto,
        CancellationToken cancellationToken = default)
    {
        var existing = await _candidatureRepository.GetByUserAndOfferAsync(
            userId,
            dto.IdOffre,
            cancellationToken);

        if (existing is not null)
        {
            return MapToDto(existing);
        }

        var entity = new CandidatureEntity
        {
            IdUtilisateur = userId,
            IdOffre = dto.IdOffre,
            InclureLettreMotivation = dto.InclureLettreMotivation,
            Statut = "EN_ATTENTE",
            DateCreation = DateTime.UtcNow
        };

        await _candidatureRepository.AddAsync(entity, cancellationToken);

        return MapToDto(entity);
    }

    public async Task<CandidatureDto?> GetByIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _candidatureRepository.GetByIdAsync(
            candidatureId,
            cancellationToken);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<List<CandidatureDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _candidatureRepository.GetByUserIdAsync(userId, cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<PagedResponse<CandidatureDto>> GetByUserIdPagedAsync(
        Guid userId,
        int offset,
        int limit,
        bool interviewOnly = false,
        CancellationToken cancellationToken = default)
    {
        var safeOffset = Math.Max(0, offset);
        var safeLimit = Math.Clamp(limit, 1, 100);

        var (items, total) = await _candidatureRepository.GetByUserIdPagedAsync(
            userId,
            safeOffset,
            safeLimit,
            interviewOnly,
            cancellationToken);

        var mapped = items.Select(MapToDto).ToList();

        return new PagedResponse<CandidatureDto>
        {
            Offset = safeOffset,
            Limit = safeLimit,
            Total = total,
            HasMore = safeOffset + mapped.Count < total,
            Items = mapped
        };
    }

    private static CandidatureDto MapToDto(CandidatureEntity entity)
    {
        return new CandidatureDto
        {
            IdCandidature           = entity.IdCandidature,
            IdUtilisateur           = entity.IdUtilisateur,
            IdOffre                 = entity.IdOffre,
            DateCreation            = entity.DateCreation,
            InclureLettreMotivation = entity.InclureLettreMotivation,
            Statut                  = entity.Statut,
            ResponseStatus          = entity.ResponseStatus,
            HasResponse             = entity.HasResponse,
            LastCheckedAtUtc        = entity.LastCheckedAtUtc,
            LastResponseAtUtc       = entity.LastResponseAtUtc,
            // AI Classification
            LastResponseFrom        = entity.LastResponseFrom,
            LastResponseSnippet     = entity.LastResponseSnippet,
            ResponseSummary         = entity.ResponseSummary,
            RecommendedAction       = entity.RecommendedAction,
            ResponseConfidence      = entity.ResponseConfidence,
            ResponseClassifiedAtUtc = entity.ResponseClassifiedAtUtc,
            // Follow-up tracking
            FollowUpNeeded          = entity.FollowUpNeeded,
            LastFollowUpAtUtc       = entity.LastFollowUpAtUtc,
        };
    }
}
