using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Email.DTOs;

public class GenerateEmailDraftDto
{
    [Required]
    public Guid CandidatureId { get; set; }

    public string EmailType { get; set; } = "application";

    public string Language { get; set; } = "fr";

    public string Tone { get; set; } = "professionnel";

    public bool IncludeMotivationLetter { get; set; } = false;

    /// <summary>
    /// Optional CV history ID from the pipeline. When provided, the email
    /// generation agent can reference the exact CV that will be attached.
    /// </summary>
    public Guid? CvHistoryId { get; set; }
}