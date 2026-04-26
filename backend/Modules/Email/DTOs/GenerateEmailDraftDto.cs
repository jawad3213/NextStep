using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Email.DTOs;

public class GenerateEmailDraftDto
{
    [Required]
    public Guid CandidatureId { get; set; }

    [Required]
    public string CandidateFullName { get; set; } = string.Empty;

    [Required]
    public string CandidateTitle { get; set; } = string.Empty;

    public List<string> Skills { get; set; } = new();
    public List<string> Highlights { get; set; } = new();

    [Required]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    public string JobTitle { get; set; } = string.Empty;

    [Required]
    public string JobSummary { get; set; } = string.Empty;

    public string Language { get; set; } = "fr";
    public string Tone { get; set; } = "professional";
    public string EmailType { get; set; } = "application";
}