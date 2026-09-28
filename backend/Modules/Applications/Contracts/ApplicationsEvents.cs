using NextStep.Shared.Events;

namespace NextStep.Modules.Applications.Contracts;

/// <summary>
/// Published after applications are deleted (single delete or with their offers).
/// Modules holding data about these applications (email drafts, coaching sessions)
/// delete it in their own schema: there are no cross-module foreign keys.
/// </summary>
public sealed record CandidaturesDeleted(Guid UserId, IReadOnlyList<Guid> CandidatureIds) : IIntegrationEvent;
