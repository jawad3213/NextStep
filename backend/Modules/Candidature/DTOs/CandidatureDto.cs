namespace NextStep.Modules.Candidature.DTOs;

public class CandidatureDto
{
    public Guid IdCandidature { get; set; }
    public Guid IdUtilisateur { get; set; }
    public Guid IdOffre { get; set; }
    public DateTime DateCreation { get; set; }
    public bool InclureLettreMotivation { get; set; }
    public string Statut { get; set; } = string.Empty;

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
}