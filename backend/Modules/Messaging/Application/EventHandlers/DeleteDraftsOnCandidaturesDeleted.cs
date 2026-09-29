using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Shared.Events;

namespace NextStep.Modules.Messaging.Application.EventHandlers;

/// <summary>Deletes the email drafts of deleted applications (replaces the former cross-module cascade).</summary>
public class DeleteDraftsOnCandidaturesDeleted(MessagingDbContext db, ILogger<DeleteDraftsOnCandidaturesDeleted> logger)
    : IIntegrationEventHandler<CandidaturesDeleted>
{
    public async Task HandleAsync(CandidaturesDeleted integrationEvent, CancellationToken ct = default)
    {
        var ids = integrationEvent.CandidatureIds;
        var deleted = await db.EmailDrafts
            .Where(d => ids.Contains(d.CandidatureId))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("Messaging - deleted {Count} email drafts of {Applications} deleted applications",
                deleted, ids.Count);
    }
}
