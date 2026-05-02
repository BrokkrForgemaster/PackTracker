using PackTracker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using PackTracker.Application.Interfaces;
using PackTracker.Application.DTOs.Request;

namespace PackTracker.Api.Services;

/// <summary name="SignalRRequestTicketNotifier">
/// Implements the IRequestTicketNotifier interface using SignalR to notify clients about updates to request tickets in real-time.
/// </summary>
public sealed class SignalRRequestTicketNotifier : IRequestTicketNotifier
{
    #region Properties
    private readonly IHubContext<RequestsHub> _hubContext;
    #endregion
    
    #region Constructor
    public SignalRRequestTicketNotifier(IHubContext<RequestsHub> hubContext)
    {
        _hubContext = hubContext;
    }
    #endregion

    #region Methods
    public Task NotifyUpdatedAsync(RequestTicketDto requestTicket, CancellationToken cancellationToken) =>
        _hubContext.Clients.All.SendAsync("RequestUpdated", requestTicket, cancellationToken);
    #endregion
}
