namespace NextStep.Modules.Email.DTOs;

public class EmailDraftDto
{
    public Guid Id { get; set; }

    public Guid CandidatureId { get; set; }

    public string EmailType { get; set; } = string.Empty;

    public string? RecipientEmail { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public bool IsSent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ProviderMessageId { get; set; }

    public int SendAttemptCount { get; set; }
}