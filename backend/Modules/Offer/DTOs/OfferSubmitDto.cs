// ============================================================
// Modules/Offer/DTOs/OfferSubmitDto.cs
// Payload reçu d'Angular pour soumettre une offre
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Offer.DTOs;

public class OfferSubmitDto
{
    /// <summary>Texte brut de l'offre collé par l'utilisateur dans l'interface Angular.</summary>
    [Required]
    [MinLength(50, ErrorMessage = "Le texte de l'offre doit contenir au moins 50 caractères.")]
    public string RawText { get; set; } = string.Empty;

    /// <summary>ID du template CV choisi : 1 = Modern, 2 = Classic, 3 = Creative.</summary>
    [Range(1, 3)]
    public int TemplateId { get; set; } = 1;
}
