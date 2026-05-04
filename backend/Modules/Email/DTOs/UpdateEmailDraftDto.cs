using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Email.DTOs;

/// <summary>
/// DTO for updating an existing email draft's editable fields.
/// Any null field means "do not change this field".
/// </summary>
public class UpdateEmailDraftDto
{
    /// <summary>
    /// The recruiter/company recipient email address.
    /// This is NOT the candidate's own email.
    /// </summary>
    public string? RecipientEmail { get; set; }

    public string? Subject { get; set; }

    public string? Body { get; set; }
}
