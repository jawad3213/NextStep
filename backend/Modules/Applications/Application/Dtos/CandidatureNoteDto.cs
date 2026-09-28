namespace NextStep.Modules.Candidature.DTOs;

public class CandidatureNoteDto
{
    public Guid Id { get; set; }
    public Guid CandidatureId { get; set; }
    public string Contenu { get; set; } = string.Empty;
    public string Auteur { get; set; } = "user";
    public DateTime CreatedAt { get; set; }
}
