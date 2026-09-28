namespace NextStep.Modules.Candidature.Models;

public class DocumentGenere
{
    public Guid IdDocument { get; set; } = Guid.NewGuid();

    public Guid IdCandidature { get; set; }
    
    public Candidature? Candidature { get; set; }

    public string? CvContenuIaJson { get; set; }

    public string? LettreMotivContenuIa { get; set; }

    public string? CheminPdfCv { get; set; }

    public string? CheminPdfLettre { get; set; }

    public int Version { get; set; } = 1;

    public DateTime DateGeneration { get; set; } = DateTime.UtcNow;
}
