using NextStep.Modules.Messaging.Domain;

namespace NextStep.Modules.Messaging.Infrastructure.Repositories;

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
    /// Returns tracked EmailDraft entities that:
    /// - are sent (IsSent = true)
    /// - have a Gmail thread ID stored
    /// - have a SentAtUtc value
    /// The reply-checking job then keeps those whose application has no reply yet
    /// (the application state is owned by the Applications module).
    /// </summary>
    Task<List<EmailDraft>> GetPendingReplyCheckAsync(
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}