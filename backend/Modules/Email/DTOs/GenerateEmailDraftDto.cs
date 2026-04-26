using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Email.DTOs;

public class GenerateEmailDraftDto
{
    [Required]
    public Guid CandidatureId { get; set; }

    public string EmailType { get; set; } = "application";

    public string Language { get; set; } = "fr";

    public string Tone { get; set; } = "professional";
}