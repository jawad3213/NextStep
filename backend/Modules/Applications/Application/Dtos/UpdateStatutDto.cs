using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Applications.Application.Dtos;

public class UpdateStatutDto
{
    [Required]
    public string NouveauStatut { get; set; } = string.Empty;

    public string? Details { get; set; }
}
