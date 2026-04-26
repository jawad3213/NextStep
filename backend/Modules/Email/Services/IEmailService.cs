using NextStep.Modules.Email.DTOs;

namespace NextStep.Modules.Email.Services;

public interface IEmailService
{
    Task<EmailDraftDto> GenerateDraftAsync(
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default);
}