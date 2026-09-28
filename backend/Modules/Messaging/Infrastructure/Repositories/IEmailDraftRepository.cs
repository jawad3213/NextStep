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

    /// <summary>
    /// Returns tracked EmailDraft entities (with related Candidature loaded) that:
    /// - are sent (IsSent = true)
    /// - have a Gmail thread ID stored
    /// - have a SentAtUtc value
    /// - belong to a candidature that has not yet received a reply (HasResponse = false)
    /// Used by the reply-checking background job.
    /// </summary>
    Task<List<EmailDraft>> GetPendingReplyCheckAsync(
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}