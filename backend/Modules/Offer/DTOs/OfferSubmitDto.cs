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
    
    public string? Titre { get; set; }
    public string? Entreprise { get; set; }

    /// <summary>ID du template CV choisi.</summary>
    public string TemplateId { get; set; } = "standard";
}
