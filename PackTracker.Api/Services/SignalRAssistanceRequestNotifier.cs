using Microsoft.AspNetCore.SignalR;
using PackTracker.Api.Hubs;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Services;

public sealed class SignalRAssistanceRequestNotifier : IAssistanceRequestNotifier
{
    private readonly IHubContext<RequestsHub> _hubContext;

    public SignalRAssistanceRequestNotifier(IHubContext<RequestsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyCreatedAsync(Guid requestId, CancellationToken cancellationToken) =>
        _hubContext.Clients.All.SendAsync("AssistanceRequestCreated", requestId, cancellationToken);

    public Task NotifyUpdatedAsync(Guid requestId, CancellationToken cancellationToken) =>
        _hubContext.Clients.All.SendAsync("AssistanceRequestUpdated", requestId, cancellationToken);
}
