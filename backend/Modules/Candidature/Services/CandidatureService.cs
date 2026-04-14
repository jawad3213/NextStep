using backend.Modules.Candidature.DTOs;
using backend.Modules.Candidature.Repositories;
using CandidatureEntity = backend.Modules.Candidature.Models.Candidature;

namespace backend.Modules.Candidature.Services;

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
        var entity = new CandidatureEntity
        {
            IdUtilisateur = userId,
            IdOffre = dto.IdOffre,
            InclureLettreMotivation = dto.InclureLettreMotivation,
            Statut = "EN_ATTENTE",
            DateCreation = DateTime.UtcNow
        };

        await _candidatureRepository.AddAsync(entity, cancellationToken);

        return new CandidatureDto
        {
            IdCandidature = entity.IdCandidature,
            IdUtilisateur = entity.IdUtilisateur,
            IdOffre = entity.IdOffre,
            DateCreation = entity.DateCreation,
            InclureLettreMotivation = entity.InclureLettreMotivation,
            Statut = entity.Statut
        };
    }

    public async Task<CandidatureDto?> GetByIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _candidatureRepository.GetByIdAsync(candidatureId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new CandidatureDto
        {
            IdCandidature = entity.IdCandidature,
            IdUtilisateur = entity.IdUtilisateur,
            IdOffre = entity.IdOffre,
            DateCreation = entity.DateCreation,
            InclureLettreMotivation = entity.InclureLettreMotivation,
            Statut = entity.Statut
        };
    }
}