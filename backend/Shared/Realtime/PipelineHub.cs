using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using NextStep.Modules.Profile.Contracts;

namespace NextStep.Shared.Realtime;

[Authorize]
public class PipelineHub(
    ILogger<PipelineHub> logger,
    IProfileApi profile,
    IOfferGroupAccess offerAccess) : Hub
{
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("PipelineHub — Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Group name = offer id, and the group now carries the analysis result, so joining
    /// must be restricted to the owner of that offer. Without this check any authenticated
    /// user could subscribe to another user's offer and read its analysis and progress.
    /// </summary>
    public async Task JoinOfferGroup(string offerId)
    {
        if (!Guid.TryParse(offerId, out var parsedOfferId))
        {
            throw new HubException("Invalid offer id.");
        }

        // The hub is [Authorize], so a principal is always present.
        var userId = await profile.EnsureUserIdAsync(Context.User!);
        var owned = await offerAccess.CanAccessOfferGroupAsync(userId, parsedOfferId, Context.ConnectionAborted);

        if (!owned)
        {
            logger.LogWarning(
                "PipelineHub — {ConnectionId} denied access to group {OfferId} (user={UserId})",
                Context.ConnectionId, offerId, userId);
            throw new HubException("Offer not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, offerId);
        logger.LogInformation("PipelineHub — {ConnectionId} joined group {OfferId}", Context.ConnectionId, offerId);
    }

    public async Task LeaveOfferGroup(string offerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, offerId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("PipelineHub — Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
