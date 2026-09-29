using NextStep.Modules.Messaging.Infrastructure.Gmail;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.CvDocuments.Contracts;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Modules.Messaging.Application.Dtos;
using NextStep.Modules.Messaging.Application.Mappings;
using NextStep.Modules.Messaging.Domain;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.Config;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Messaging.Application.Services;

/// <summary>Loads a draft and checks that its application belongs to the user.</summary>
public class EmailDraftOwnership(MessagingDbContext db, IApplicationsApi applications)
{
    private readonly MessagingDbContext _db = db;
    private readonly IApplicationsApi _applications = applications;

    /// <summary>Throws <see cref="ForbiddenException"/> unless the application belongs to the user.</summary>
    public async Task EnsureApplicationOwnedAsync(Guid candidatureId, Guid localUserId, CancellationToken cancellationToken)
    {
        var candidature = await _applications.GetApplicationAsync(candidatureId, cancellationToken)
            ?? throw new NotFoundException($"Candidature {candidatureId} not found.");

        if (candidature.UserId != localUserId)
            throw new ForbiddenException($"User {localUserId} does not own candidature {candidatureId}.");
    }

    public async Task<EmailDraft> LoadOwnedAsync(
        Guid draftId,
        Guid localUserId,
        CancellationToken cancellationToken)
    {
        var draft = await _db.EmailDrafts
            .FirstOrDefaultAsync(d => d.Id == draftId, cancellationToken);

        if (draft is null)
            throw new NotFoundException($"Email draft {draftId} not found.");

        var candidature = await _applications.GetApplicationAsync(
            draft.CandidatureId, cancellationToken);

        if (candidature is null)
            throw new NotFoundException(
                $"Candidature {draft.CandidatureId} not found for draft {draftId}.");

        if (candidature.UserId != localUserId)
            throw new ForbiddenException(
                $"User {localUserId} does not own draft {draftId}.");

        return draft;
    }
}
