using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Repositories;

public interface IEmailDraftRepository
{
    Task<EmailDraft> AddAsync(
        EmailDraft draft,
        CancellationToken cancellationToken = default);

    Task<EmailDraft?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<EmailDraft>> GetByCandidatureIdAsync(
        Guid candidatureId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}