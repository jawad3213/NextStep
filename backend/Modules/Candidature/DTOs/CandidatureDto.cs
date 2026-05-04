namespace NextStep.Modules.Candidature.DTOs;

public class CandidatureDto
{
    public Guid IdCandidature { get; set; }
    public Guid IdUtilisateur { get; set; }
    public Guid IdOffre { get; set; }
    public DateTime DateCreation { get; set; }
    public bool InclureLettreMotivation { get; set; }
    public string Statut { get; set; } = string.Empty;
}