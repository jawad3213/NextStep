namespace NextStep.Modules.Candidature.DTOs;

public class CandidatureStatusHistoryDto
{
    public Guid Id { get; set; }
    public Guid CandidatureId { get; set; }
    public string? AncienStatut { get; set; }
    public string NouveauStatut { get; set; } = string.Empty;
    public string Source { get; set; } = "user";
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
