using NextStep.Modules.Candidature.DTOs;
using NextStep.Modules.Candidature.Repositories;
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
        };
    }
}