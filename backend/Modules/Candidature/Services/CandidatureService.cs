using NextStep.Modules.Candidature.DTOs;
using NextStep.Modules.Candidature.Models;
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
        if (dto.IdOffre.HasValue)
        {
            var existing = await _candidatureRepository.GetByUserAndOfferAsync(
                userId,
                dto.IdOffre.Value,
                cancellationToken);

            if (existing is not null)
            {
                return MapToDto(existing);
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

        return MapToDto(entity);
    }

    public async Task<CandidatureDto?> GetByIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _candidatureRepository.GetByIdAsync(
            candidatureId,
            cancellationToken);

        if (entity is null) return null;

        var dto = MapToDto(entity);
        dto.CandidatureNotes = (await _candidatureRepository.GetNotesAsync(candidatureId, cancellationToken))
            .Select(n => new CandidatureNoteDto
            {
                Id = n.Id,
                CandidatureId = n.CandidatureId,
                Contenu = n.Contenu,
                Auteur = n.Auteur,
                CreatedAt = n.CreatedAt
            }).ToList();
        dto.StatusHistoryEntries = (await _candidatureRepository.GetHistoryAsync(candidatureId, cancellationToken))
            .Select(h => new CandidatureStatusHistoryDto
            {
                Id = h.Id,
                CandidatureId = h.CandidatureId,
                AncienStatut = h.AncienStatut,
                NouveauStatut = h.NouveauStatut,
                Source = h.Source,
                Details = h.Details,
                CreatedAt = h.CreatedAt
            }).ToList();

        return dto;
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

        return MapToDto(entity);
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

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _candidatureRepository.GetByIdAsync(candidatureId, cancellationToken);
        if (entity is null) return false;

        await _candidatureRepository.DeleteAsync(entity, cancellationToken);
        return true;
    }

    public async Task<List<CandidatureNoteDto>> GetNotesAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var notes = await _candidatureRepository.GetNotesAsync(candidatureId, cancellationToken);
        return notes.Select(n => new CandidatureNoteDto
        {
            Id = n.Id,
            CandidatureId = n.CandidatureId,
            Contenu = n.Contenu,
            Auteur = n.Auteur,
            CreatedAt = n.CreatedAt
        }).ToList();
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

        return new CandidatureNoteDto
        {
            Id = note.Id,
            CandidatureId = note.CandidatureId,
            Contenu = note.Contenu,
            Auteur = note.Auteur,
            CreatedAt = note.CreatedAt
        };
    }

    public async Task<List<CandidatureStatusHistoryDto>> GetHistoryAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default)
    {
        var history = await _candidatureRepository.GetHistoryAsync(candidatureId, cancellationToken);
        return history.Select(h => new CandidatureStatusHistoryDto
        {
            Id = h.Id,
            CandidatureId = h.CandidatureId,
            AncienStatut = h.AncienStatut,
            NouveauStatut = h.NouveauStatut,
            Source = h.Source,
            Details = h.Details,
            CreatedAt = h.CreatedAt
        }).ToList();
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
            Channel                 = entity.Channel,
            ChannelUrl              = entity.ChannelUrl,
            ChannelContact          = entity.ChannelContact,
            ApplicationDate         = entity.ApplicationDate,
            AppliedManually         = entity.AppliedManually,
            OfferSource             = entity.OfferSource,
            Notes                   = entity.Notes,
            Language                = entity.Language,
            ResponseStatus          = entity.ResponseStatus,
            HasResponse             = entity.HasResponse,
            LastCheckedAtUtc        = entity.LastCheckedAtUtc,
            LastResponseAtUtc       = entity.LastResponseAtUtc,
            LastResponseFrom        = entity.LastResponseFrom,
            LastResponseSnippet     = entity.LastResponseSnippet,
            ResponseSummary         = entity.ResponseSummary,
            RecommendedAction       = entity.RecommendedAction,
            ResponseConfidence      = entity.ResponseConfidence,
            ResponseClassifiedAtUtc = entity.ResponseClassifiedAtUtc,
            FollowUpNeeded          = entity.FollowUpNeeded,
            LastFollowUpAtUtc       = entity.LastFollowUpAtUtc,
        };
    }
}
