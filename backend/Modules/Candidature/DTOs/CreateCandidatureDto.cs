using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Candidature.DTOs;

public class CreateCandidatureDto
{
    [Required]
    public Guid IdOffre { get; set; }

    public bool InclureLettreMotivation { get; set; } = false;
}