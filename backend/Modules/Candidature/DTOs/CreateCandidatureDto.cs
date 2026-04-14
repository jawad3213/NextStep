using System.ComponentModel.DataAnnotations;

namespace backend.Modules.Candidature.DTOs;

public class CreateCandidatureDto
{
    [Required]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    public string JobTitle { get; set; } = string.Empty;

    [Required]
    public string JobOfferText { get; set; } = string.Empty;
}