using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace NextStep.SignalR;

[Authorize]
public class PipelineHub : Hub
{
    private readonly ILogger<PipelineHub> _logger;

    public PipelineHub(ILogger<PipelineHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("PipelineHub — Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public async Task JoinOfferGroup(string offerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, offerId);
        _logger.LogInformation("PipelineHub — {ConnectionId} joined group {OfferId}", Context.ConnectionId, offerId);
    }

    public async Task LeaveOfferGroup(string offerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, offerId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("PipelineHub — Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
