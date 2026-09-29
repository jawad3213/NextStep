namespace NextStep.Modules.Applications.Contracts;

/// <summary>
/// Public contract of the Applications module (offers, applications, application documents).
/// Other modules must use this interface instead of Applications' entities, repositories or
/// services, so the module can later become a separate service (this interface then becomes
/// an HTTP/gRPC client) without changing its callers.
/// </summary>
public interface IApplicationsApi
{
    /// <summary>Analysis summary of one of the user's offers, or null if not analysed / not found.</summary>
    Task<OfferSummary?> GetOfferSummaryAsync(Guid userId, Guid offerId, CancellationToken ct = default);

    /// <summary>Throws <see cref="KeyNotFoundException"/> if the offer does not exist or is not the user's.</summary>
    Task EnsureOfferOwnedAsync(Guid userId, Guid offerId, CancellationToken ct = default);

    /// <summary>The CV JSON stored on the user's application for this offer, or null.</summary>
    Task<StoredCvDocument?> GetCvDocumentAsync(Guid userId, Guid offerId, CancellationToken ct = default);

    /// <summary>
    /// Stores CV JSON on the application's document (version + 1).
    /// <paramref name="createApplicationIfMissing"/>: create the application when the user owns the
    /// offer but has none yet; otherwise nothing is stored when there is no application.
    /// <paramref name="pdfUrl"/>: saved PDF location; null keeps the current one.
    /// Returns null when nothing was stored.
    /// </summary>
    Task<StoredCvDocument?> SaveCvDocumentAsync(
        Guid userId,
        Guid offerId,
        string cvJson,
        string? pdfUrl,
        bool createApplicationIfMissing,
        CancellationToken ct = default);

    // ── Offers ──────────────────────────────────────────────────────────────

    Task<bool> OfferExistsAsync(Guid offerId, CancellationToken ct = default);

    /// <summary>Raw text and stored analysis JSON of an offer (no ownership check), or null.</summary>
    Task<OfferContent?> GetOfferContentAsync(Guid offerId, CancellationToken ct = default);

    /// <summary>Saves a new offer for the user from its raw text; returns the offer id.</summary>
    Task<Guid> CreateOfferFromTextAsync(Guid userId, string rawText, CancellationToken ct = default);

    // ── Applications (candidatures) ─────────────────────────────────────────

    Task<ApplicationSnapshot?> GetApplicationAsync(Guid candidatureId, CancellationToken ct = default);

    Task<IReadOnlyList<ApplicationSnapshot>> GetApplicationsAsync(IReadOnlyCollection<Guid> candidatureIds, CancellationToken ct = default);

    Task<ApplicationSnapshot?> FindApplicationForOfferAsync(Guid userId, Guid offerId, CancellationToken ct = default);

    /// <summary>The user's oldest application, if any.</summary>
    Task<ApplicationSnapshot?> FindFirstApplicationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Ids of the offers the user has applied to.</summary>
    Task<IReadOnlyList<Guid>> ListAppliedOfferIdsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Makes sure the user has an application for the offer. Creates the offer (from
    /// <paramref name="offerTextIfMissing"/>) when it does not exist, then the application
    /// with <paramref name="status"/> when missing. Returns the application id.
    /// </summary>
    Task<Guid> EnsureApplicationForOfferAsync(
        Guid userId,
        Guid offerId,
        string offerTextIfMissing,
        string status,
        CancellationToken ct = default);

    // ── Response tracking (updated by Messaging) ────────────────────────────

    /// <summary>Applications with no recruiter reply that are not already flagged for or in follow-up.</summary>
    Task<IReadOnlyList<ApplicationSnapshot>> ListAwaitingFollowUpCheckAsync(CancellationToken ct = default);

    /// <summary>Sets both the application status and its response status (follow-up workflow).</summary>
    Task SetFollowUpStatusAsync(Guid candidatureId, string status, CancellationToken ct = default);

    Task MarkFollowUpNeededAsync(Guid candidatureId, DateTime? lastFollowUpAtUtc, CancellationToken ct = default);

    Task RecordReplyCheckAsync(Guid candidatureId, DateTime checkedAtUtc, CancellationToken ct = default);

    Task RecordRecruiterReplyAsync(Guid candidatureId, RecruiterReply reply, CancellationToken ct = default);
}

public sealed record OfferSummary(
    Guid OfferId,
    string Title,
    string? Company,
    string? Description,
    IReadOnlyList<string> RequiredSkills,
    IReadOnlyList<string> AtsKeywords,
    string? Location = null,
    string? ContractType = null,
    int? YearsExperience = null,
    int? MatchingScore = null,   // 0–100, null when the offer was not matched yet
    DateTime? AnalysedAt = null);

public sealed record StoredCvDocument(string CvJson, int Version, DateTime UpdatedAtUtc);

public sealed record OfferContent(Guid OfferId, Guid OwnerUserId, string RawText, string? AnalysisJson);

/// <summary>Read model of an application, as seen by other modules.</summary>
public sealed record ApplicationSnapshot(
    Guid CandidatureId,
    Guid UserId,
    Guid? OfferId,
    string Status,
    string ResponseStatus,
    bool HasResponse,
    double? ResponseConfidence,
    string? LastResponseFrom,
    string? LastResponseSnippet,
    DateTime? LastResponseAtUtc,
    string? ResponseSummary,
    string? RecommendedAction);

/// <summary>A detected recruiter reply and its (optional) AI classification.</summary>
public sealed record RecruiterReply(
    string Status,
    DateTime CheckedAtUtc,
    DateTime? ReplyAtUtc,
    string? From,
    string? Snippet,
    string? Summary,
    string? RecommendedAction,
    double? Confidence,
    DateTime? ClassifiedAtUtc);
