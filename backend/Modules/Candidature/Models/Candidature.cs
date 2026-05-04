using NextStep.Modules.Email.Models;
using NextStep.Modules.Offer.Models;

namespace NextStep.Modules.Candidature.Models;

public class Candidature
{
    public Guid IdCandidature { get; set; } = Guid.NewGuid();

    public Guid IdUtilisateur { get; set; }

    public Guid IdOffre { get; set; }

    public OffreEmploi? Offre { get; set; }

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public bool InclureLettreMotivation { get; set; } = false;

    public string Statut { get; set; } = "EN_ATTENTE";

    public ICollection<EmailDraft> EmailDrafts { get; set; } = new List<EmailDraft>();
}