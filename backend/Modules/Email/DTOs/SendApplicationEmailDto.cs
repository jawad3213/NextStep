using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Email.DTOs;

public class SendApplicationEmailDto
{
    [Required]
    public Guid OfferId { get; set; }

    public Guid? CvHistoryId { get; set; }

    [Required]
    [EmailAddress]
    public string RecipientEmail { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public string EmailType { get; set; } = "application";

    public string Language { get; set; } = "fr";
}
