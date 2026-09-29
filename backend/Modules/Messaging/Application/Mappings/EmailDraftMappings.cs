using NextStep.Modules.Messaging.Application.Dtos;
using NextStep.Modules.Messaging.Domain;

namespace NextStep.Modules.Messaging.Application.Mappings;

public static class EmailDraftMappings
{
    public static EmailDraftDto ToDto(this EmailDraft draft) => new()
    {
        Id                = draft.Id,
        CandidatureId     = draft.CandidatureId,
        EmailType         = draft.EmailType,
        RecipientEmail    = draft.RecipientEmail,
        Subject           = draft.Subject,
        Body              = draft.Body,
        Language          = draft.Language,
        IsApproved        = draft.IsApproved,
        IsSent            = draft.IsSent,
        CreatedAtUtc      = draft.CreatedAtUtc,
        UpdatedAtUtc      = draft.UpdatedAtUtc,
        ApprovedAtUtc     = draft.ApprovedAtUtc,
        SentAtUtc         = draft.SentAtUtc,
        ErrorMessage      = draft.ErrorMessage,
        ProviderMessageId = draft.ProviderMessageId,
        ProviderThreadId  = draft.ProviderThreadId,
        SendAttemptCount  = draft.SendAttemptCount,
    };
}
