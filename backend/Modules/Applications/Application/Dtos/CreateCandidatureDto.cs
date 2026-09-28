using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Candidature.DTOs;

public class CreateCandidatureDto
{
    public Guid? IdOffre { get; set; }

    public string? Entreprise { get; set; }

    public string? Poste { get; set; }

    [Required]
    public string Channel { get; set; } = "EMAIL";

    public string? ChannelUrl { get; set; }

    public string? ChannelContact { get; set; }

    public DateTime? ApplicationDate { get; set; }

    public bool AppliedManually { get; set; } = false;

    public bool InclureLettreMotivation { get; set; } = false;

    public string Language { get; set; } = "AUTO";

    public string? OfferSource { get; set; }

    public string? Notes { get; set; }
}