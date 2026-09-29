// ============================================================
// Modules/Applications/DTOs/OfferSubmitDto.cs
// Payload received from Angular to submit an offer
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Applications.Application.Dtos;

public class OfferSubmitDto
{
    /// <summary>Raw text of the job offer pasted by the user in the Angular interface.</summary>
    [Required]
    [MinLength(50, ErrorMessage = "The offer text must contain at least 50 characters.")]
    public string RawText { get; set; } = string.Empty;
    
    public string? Titre { get; set; }
    public string? Entreprise { get; set; }

    /// <summary>ID of the chosen CV template.</summary>
    public int TemplateId { get; set; } = 1;
}
