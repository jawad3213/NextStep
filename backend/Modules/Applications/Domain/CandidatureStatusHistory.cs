namespace NextStep.Modules.Applications.Domain;

public class CandidatureStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CandidatureId { get; set; }

    public string? AncienStatut { get; set; }

    public string NouveauStatut { get; set; } = string.Empty;

    public string Source { get; set; } = "user"; // "user" | "ai" | "system" | "email_reply"

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Candidature? Candidature { get; set; }
}
