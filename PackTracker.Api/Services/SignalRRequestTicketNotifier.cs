using Microsoft.AspNetCore.SignalR;
using PackTracker.Api.Hubs;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Services;

public sealed class SignalRRequestTicketNotifier : IRequestTicketNotifier
{
    private readonly IHubContext<RequestsHub> _hubContext;

    public SignalRRequestTicketNotifier(IHubContext<RequestsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyUpdatedAsync(RequestTicketDto requestTicket, CancellationToken cancellationToken) =>
        _hubContext.Clients.All.SendAsync("RequestUpdated", requestTicket, cancellationToken);
}
