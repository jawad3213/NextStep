using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Email.DTOs;

/// <summary>
/// Request body for POST /api/emails/generate-follow-up.
/// The delay threshold is NOT enforced here — that is handled by DetectFollowUpNeededJob.
/// This endpoint is user-triggered and allows early follow-up generation.
/// </summary>
public class GenerateFollowUpDraftDto
{
    /// <summary>ID of the candidature to generate a follow-up for.</summary>
    [Required]
    public Guid CandidatureId { get; set; }

    /// <summary>Language for the follow-up email. Default: "fr".</summary>
    public string Language { get; set; } = "fr";

    /// <summary>Tone for the follow-up email. Default: "professionnel".</summary>
    public string Tone { get; set; } = "professionnel";
}
