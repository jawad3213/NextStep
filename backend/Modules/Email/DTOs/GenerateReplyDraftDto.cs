using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Email.DTOs;

/// <summary>
/// Request body for <c>POST /api/emails/generate-reply</c>.
/// Triggers AI generation of a reply draft addressed to the recruiter
/// who responded to this candidature.
/// </summary>
public class GenerateReplyDraftDto
{
    /// <summary>The candidature to generate a reply for. Must have HasResponse = true.</summary>
    [Required]
    public Guid CandidatureId { get; set; }

    /// <summary>Language of the generated reply. Defaults to French.</summary>
    public string Language { get; set; } = "fr";

    /// <summary>Tone of the generated reply. Defaults to professional.</summary>
    public string Tone { get; set; } = "professionnel";

    /// <summary>
    /// Optional free-text instructions from the user that the AI should follow
    /// when compatible with known facts and the recruiter's context.
    /// Examples:
    ///   "Mentionner que je suis disponible lundi après-midi et mercredi matin."
    ///   "Garder la réponse courte."
    ///   "Répondre en français."
    ///   "Préciser que je peux fournir mon GitHub si nécessaire."
    /// </summary>
    public string? UserInstructions { get; set; }
}
