using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;

namespace NextStep.Modules.Email.Models;

public class EmailDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CandidatureId { get; set; }

    public string EmailType { get; set; } = "application";

    public string? RecipientEmail { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Language { get; set; } = "fr";

    public bool IsApproved { get; set; } = false;

    public bool IsSent { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Gmail message ID returned by the Gmail API after successful sending.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Number of send attempts made (incremented on each real attempt).</summary>
    public int SendAttemptCount { get; set; } = 0;

    public CandidatureEntity? Candidature { get; set; }
}