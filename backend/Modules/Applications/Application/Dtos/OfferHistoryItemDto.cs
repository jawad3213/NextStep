namespace NextStep.Modules.Offer.DTOs;

public class OfferHistoryItemDto
{
    public Guid OfferId { get; set; }
    public string Titre { get; set; } = "Offre";
    public string Entreprise { get; set; } = "";
    public string Localisation { get; set; } = "";
    public int? ScoreMatching { get; set; }
    public string Status { get; set; } = "non_traitee";
    public int CurrentStep { get; set; } = 1;
    public DateTime DateCreation { get; set; }
}
