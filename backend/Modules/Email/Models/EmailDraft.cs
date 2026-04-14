using CandidatureEntity = backend.Modules.Candidature.Models.Candidature;

namespace backend.Modules.Email.Models;

public class EmailDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidatureId { get; set; }
    public string EmailType { get; set; } = "application"; // application | followup
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Language { get; set; } = "fr";
    public bool IsApproved { get; set; } = false;
    public bool IsSent { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public CandidatureEntity? Candidature { get; set; }
}