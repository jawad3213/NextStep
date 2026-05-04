using NextStep.Modules.Email.DTOs;

namespace NextStep.Modules.Email.Services;

public interface IEmailService
{
    Task<EmailDraftDto> GenerateDraftAsync(
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default);

    Task<List<EmailDraftDto>> GetDraftsByCandidatureAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);

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
}