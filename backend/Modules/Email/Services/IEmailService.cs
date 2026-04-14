using backend.Modules.Email.DTOs;

namespace backend.Modules.Email.Services;

public interface IEmailService
{
    Task<EmailDraftDto> GenerateDraftAsync(
        GenerateEmailDraftDto dto,
        CancellationToken cancellationToken = default);
}