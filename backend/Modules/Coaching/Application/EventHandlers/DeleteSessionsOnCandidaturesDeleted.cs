using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Coaching.Infrastructure.Persistence;
using NextStep.Shared.Events;

namespace NextStep.Modules.Coaching.Application.EventHandlers;

/// <summary>
/// Deletes the interview sessions of deleted applications (replaces the former cross-module cascade).
/// Their questions go with them (foreign key inside the coaching schema).
/// </summary>
public class DeleteSessionsOnCandidaturesDeleted(CoachingDbContext db, ILogger<DeleteSessionsOnCandidaturesDeleted> logger)
    : IIntegrationEventHandler<CandidaturesDeleted>
{
    public async Task HandleAsync(CandidaturesDeleted integrationEvent, CancellationToken ct = default)
    {
        var ids = integrationEvent.CandidatureIds;
        var deleted = await db.SessionCoachings
            .Where(s => s.IdCandidature.HasValue && ids.Contains(s.IdCandidature.Value))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("Coaching - deleted {Count} sessions of {Applications} deleted applications",
                deleted, ids.Count);
    }
}
