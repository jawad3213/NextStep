namespace NextStep.Modules.Candidature.DTOs;

public class CandidatureDto
{
    public Guid IdCandidature { get; set; }
    public Guid IdUtilisateur { get; set; }
    public Guid? IdOffre { get; set; }
    public DateTime DateCreation { get; set; }
    public bool InclureLettreMotivation { get; set; }
    public string Statut { get; set; } = string.Empty;

    // ── Multi-channel tracking ────────────────────────────────────────────────────
    public string Channel { get; set; } = "EMAIL";
    public string? ChannelUrl { get; set; }
    public string? ChannelContact { get; set; }
    public DateTime ApplicationDate { get; set; }
    public bool AppliedManually { get; set; }
    public string? OfferSource { get; set; }
    public string? Notes { get; set; }
    public string Language { get; set; } = "AUTO";

    // ── Email reply tracking ──────────────────────────────────────────────────────
    public string ResponseStatus { get; set; } = "EN_ATTENTE";
    public bool HasResponse { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
    public DateTime? LastResponseAtUtc { get; set; }

    // ── AI Classification ─────────────────────────────────────────────────────────
    public string? LastResponseFrom { get; set; }
    public string? LastResponseSnippet { get; set; }
    public string? ResponseSummary { get; set; }
    public string? RecommendedAction { get; set; }
    public double? ResponseConfidence { get; set; }
    public DateTime? ResponseClassifiedAtUtc { get; set; }

    // ── Follow-up tracking ────────────────────────────────────────────────────────
    public bool FollowUpNeeded { get; set; }
    public DateTime? LastFollowUpAtUtc { get; set; }

    // ── Notes & History ───────────────────────────────────────────────────────────
    public List<CandidatureNoteDto> CandidatureNotes { get; set; } = new();
    public List<CandidatureStatusHistoryDto> StatusHistoryEntries { get; set; } = new();
}