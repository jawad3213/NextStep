using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Domain;

namespace NextStep.Modules.Applications.Application.Mappings;

/// <summary>Entity → DTO mappings of the applications (candidatures) feature.</summary>
public static class CandidatureMappings
{
    public static CandidatureDto ToDto(this Candidature entity) => new()
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

    public static CandidatureNoteDto ToDto(this CandidatureNote note) => new()
    {
        Id            = note.Id,
        CandidatureId = note.CandidatureId,
        Contenu       = note.Contenu,
        Auteur        = note.Auteur,
        CreatedAt     = note.CreatedAt
    };

    public static CandidatureStatusHistoryDto ToDto(this CandidatureStatusHistory history) => new()
    {
        Id            = history.Id,
        CandidatureId = history.CandidatureId,
        AncienStatut  = history.AncienStatut,
        NouveauStatut = history.NouveauStatut,
        Source        = history.Source,
        Details       = history.Details,
        CreatedAt     = history.CreatedAt
    };
}
