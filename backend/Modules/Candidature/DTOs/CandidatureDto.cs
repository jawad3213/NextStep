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
}