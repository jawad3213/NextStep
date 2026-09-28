using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Mappings;
using NextStep.Modules.Applications.Domain;
using NextStep.Modules.Applications.Infrastructure.Repositories;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Events;
using NextStep.Shared.Pagination;
using CandidatureEntity = NextStep.Modules.Applications.Domain.Candidature;

namespace NextStep.Modules.Applications.Application.Services;

public class CandidatureService : ICandidatureService
{
    private readonly ICandidatureRepository _candidatureRepository;
    private readonly IEventPublisher _events;

    public CandidatureService(ICandidatureRepository candidatureRepository, IEventPublisher events)
    {
        _candidatureRepository = candidatureRepository;
        _events = events;
    }

    public async Task<CandidatureDto> CreateAsync(
        Guid userId,
        CreateCandidatureDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.IdOffre.HasValue)
        {
            var existing = await _candidatureRepository.GetByUserAndOfferAsync(
                userId,
                dto.IdOffre.Value,
                cancellationToken);

            if (existing is not null)
            {
                return existing.ToDto();
            }
        }

        var entity = new CandidatureEntity
        {
            IdUtilisateur = userId,
            IdOffre = dto.IdOffre,
            InclureLettreMotivation = dto.InclureLettreMotivation,
            Statut = dto.AppliedManually ? "ENVOYE" : "BROUILLON",
            Channel = dto.Channel,
            ChannelUrl = dto.ChannelUrl,
            ChannelContact = dto.ChannelContact,
            ApplicationDate = dto.ApplicationDate ?? DateTime.UtcNow,
            AppliedManually = dto.AppliedManually,
            OfferSource = dto.OfferSource,
            Notes = dto.Notes,
            Language = dto.Language,
            DateCreation = DateTime.UtcNow
        };

        await _candidatureRepository.AddAsync(entity, cancellationToken);

        await _candidatureRepository.AddHistoryAsync(new CandidatureStatusHistory
        {
            CandidatureId = entity.IdCandidature,
            AncienStatut = null,
            NouveauStatut = entity.Statut,
            Source = "user",
            Details = "Candidature créée",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return entity.ToDto();
    }

    public async Task<CandidatureDto?> GetByIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _candidatureRepository.GetByIdAsync(
            candidatureId,
            cancellationToken);

        if (entity is null) return null;

        var dto = entity.ToDto();
        dto.CandidatureNotes = (await _candidatureRepository.GetNotesAsync(candidatureId, cancellationToken))
            .Select(n => n.ToDto()).ToList();
        dto.StatusHistoryEntries = (await _candidatureRepository.GetHistoryAsync(candidatureId, cancellationToken))
            .Select(h => h.ToDto()).ToList();

        return dto;
    }

    public async Task<List<CandidatureDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _candidatureRepository.GetByUserIdAsync(userId, cancellationToken);
        return entities.Select(e => e.ToDto()).ToList();
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

        var mapped = items.Select(e => e.ToDto()).ToList();

        return new PagedResponse<CandidatureDto>
        {
            Offset = safeOffset,
            Limit = safeLimit,
            Total = total,
            HasMore = safeOffset + mapped.Count < total,
            Items = mapped
        };
    }

    public async Task<CandidatureDto?> UpdateStatutAsync(
        Guid candidatureId,
        UpdateStatutDto dto,
        CancellationToken cancellationToken = default)
    {
        var entity = await _candidatureRepository.GetByIdAsync(candidatureId, cancellationToken);
        if (entity is null) return null;

        var ancienStatut = entity.Statut;
        entity.Statut = dto.NouveauStatut;
        await _candidatureRepository.UpdateAsync(entity, cancellationToken);

        await _candidatureRepository.AddHistoryAsync(new CandidatureStatusHistory
        {
            CandidatureId = candidatureId,
            AncienStatut = ancienStatut,
            NouveauStatut = dto.NouveauStatut,
            Source = "user",
            Details = dto.Details,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return entity.ToDto();
    }

    public async Task<CandidatureDto?> UpdateAsync(
        Guid candidatureId,
        UpdateCandidatureDto dto,
        CancellationToken cancellationToken = default)
    {
        var entity = await _candidatureRepository.GetByIdAsync(candidatureId, cancellationToken);
        if (entity is null) return null;

        if (dto.Channel is not null) entity.Channel = dto.Channel;
        if (dto.ChannelUrl is not null) entity.ChannelUrl = dto.ChannelUrl;
        if (dto.ChannelContact is not null) entity.ChannelContact = dto.ChannelContact;
        if (dto.Notes is not null) entity.Notes = dto.Notes;
        if (dto.Language is not null) entity.Language = dto.Language;
        if (dto.InclureLettreMotivation.HasValue) entity.InclureLettreMotivation = dto.InclureLettreMotivation.Value;

        await _candidatureRepository.UpdateAsync(entity, cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _candidatureRepository.GetByIdAsync(candidatureId, cancellationToken);
        if (entity is null) return false;

        await _candidatureRepository.DeleteAsync(entity, cancellationToken);

        // Other modules (email drafts, coaching sessions) clean up their own data.
        await _events.PublishAsync(
            new CandidaturesDeleted(entity.IdUtilisateur, [entity.IdCandidature]), cancellationToken);
        return true;
    }

    public async Task<CandidatureDto> GetOwnedAsync(
        Guid userId,
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var candidature = await GetByIdAsync(candidatureId, cancellationToken)
            ?? throw new NotFoundException("Candidature introuvable.");

        if (candidature.IdUtilisateur != userId)
            throw new ForbiddenException("Accès refusé à cette candidature.");

        return candidature;
    }

    public async Task<List<CandidatureNoteDto>> GetNotesAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var notes = await _candidatureRepository.GetNotesAsync(candidatureId, cancellationToken);
        return notes.Select(n => n.ToDto()).ToList();
    }

    public async Task<CandidatureNoteDto> AddNoteAsync(
        Guid candidatureId,
        AddNoteDto dto,
        CancellationToken cancellationToken = default)
    {
        var note = new CandidatureNote
        {
            CandidatureId = candidatureId,
            Contenu = dto.Contenu,
            Auteur = dto.Auteur,
            CreatedAt = DateTime.UtcNow
        };

        await _candidatureRepository.AddNoteAsync(note, cancellationToken);

        return note.ToDto();
    }

    public async Task<List<CandidatureStatusHistoryDto>> GetHistoryAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var history = await _candidatureRepository.GetHistoryAsync(candidatureId, cancellationToken);
        return history.Select(h => h.ToDto()).ToList();
    }
}
