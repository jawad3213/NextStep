using backend.Modules.Email.Models;

namespace backend.Modules.Email.Repositories;

public interface IEmailDraftRepository
{
    Task<EmailDraft> AddAsync(
        EmailDraft draft,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}