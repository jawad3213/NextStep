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
            UserId = userId,
            CompanyName = dto.CompanyName.Trim(),
            JobTitle = dto.JobTitle.Trim(),
            JobOfferText = dto.JobOfferText.Trim(),
            Status = "DRAFT",
            CreatedAtUtc = DateTime.UtcNow
        };

        await _candidatureRepository.AddAsync(entity, cancellationToken);

        return new CandidatureDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            CompanyName = entity.CompanyName,
            JobTitle = entity.JobTitle,
            Status = entity.Status,
            CreatedAtUtc = entity.CreatedAtUtc
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
            Id = entity.Id,
            UserId = entity.UserId,
            CompanyName = entity.CompanyName,
            JobTitle = entity.JobTitle,
            Status = entity.Status,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }
}