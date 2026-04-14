using backend.Modules.Email.Models;

namespace backend.Modules.Candidature.Models;

public class Candidature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string JobOfferText { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<EmailDraft> EmailDrafts { get; set; } = new List<EmailDraft>();
}