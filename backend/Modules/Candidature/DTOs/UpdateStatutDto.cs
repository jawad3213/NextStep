using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Candidature.DTOs;

public class UpdateStatutDto
{
    [Required]
    public string NouveauStatut { get; set; } = string.Empty;

    public string? Details { get; set; }
}
