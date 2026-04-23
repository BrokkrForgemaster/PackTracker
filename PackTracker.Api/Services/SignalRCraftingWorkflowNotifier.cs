using Microsoft.AspNetCore.SignalR;
using PackTracker.Api.Hubs;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Services;

public sealed class SignalRCraftingWorkflowNotifier : ICraftingWorkflowNotifier
{
    private readonly IHubContext<RequestsHub> _hubContext;

    public SignalRCraftingWorkflowNotifier(IHubContext<RequestsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAsync(string eventName, Guid requestId, CancellationToken cancellationToken) =>
        _hubContext.Clients.All.SendAsync(eventName, requestId, cancellationToken);

    public async Task NotifyClaimedAsync(
        string requesterDiscordId,
        string claimerDiscordId,
        string claimerDisplayName,
        string requesterDisplayName,
        Guid requestId,
        string requestType,
        string requestLabel,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            RequestId = requestId,
            RequestType = requestType,
            RequestLabel = requestLabel,
            ClaimerDisplayName = claimerDisplayName,
            RequesterDisplayName = requesterDisplayName
        };

        if (!string.IsNullOrWhiteSpace(requesterDiscordId) && requesterDiscordId != claimerDiscordId)
            await _hubContext.Clients.User(requesterDiscordId)
                .SendAsync("RequestClaimed", payload, cancellationToken);

        if (!string.IsNullOrWhiteSpace(claimerDiscordId))
            await _hubContext.Clients.User(claimerDiscordId)
                .SendAsync("ClaimConfirmed", payload, cancellationToken);
    }
}
