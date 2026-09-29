namespace NextStep.Modules.Applications.Domain;

public class CandidatureNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CandidatureId { get; set; }

    public string Contenu { get; set; } = string.Empty;

    public string Auteur { get; set; } = "user"; // "user" | "ai"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Candidature? Candidature { get; set; }
}
