using NextStep.Modules.Email.DTOs;

namespace NextStep.Modules.Email.Services;

public interface IEmailService
{
    Task<EmailDraftDto> GenerateDraftAsync(
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default);

<<<<<<< HEAD
    Task<EmailDraftDto> GetDraftByIdAsync(
        Guid draftId,
        Guid localUserId,
=======
    Task<EmailDraftDto> SendApplicationEmailAsync(
        Guid userId,
        SendApplicationEmailDto dto,
>>>>>>> dd6b6da26fce12d583ed2393398e6269c90a5938
        CancellationToken cancellationToken = default);

    Task<List<EmailDraftDto>> GetDraftsByCandidatureAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);
<<<<<<< HEAD

    Task<EmailDraftDto> UpdateDraftAsync(
        Guid draftId,
        Guid localUserId,
        UpdateEmailDraftDto dto,
        CancellationToken cancellationToken = default);

    Task<EmailDraftDto> ApproveDraftAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken = default);

    Task<SendEmailResultDto> SendDraftAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken = default);

    Task<EmailDraftDto> GenerateFollowUpDraftAsync(
        GenerateFollowUpDraftDto dto,
        Guid localUserId,
        CancellationToken cancellationToken = default);

    Task<EmailDraftDto> GenerateReplyDraftAsync(
        GenerateReplyDraftDto dto,
        Guid localUserId,
        CancellationToken cancellationToken = default);
}
=======
}
>>>>>>> dd6b6da26fce12d583ed2393398e6269c90a5938
