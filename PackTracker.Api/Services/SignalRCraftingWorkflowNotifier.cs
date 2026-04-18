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
}
