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

    public async Task NotifyClaimedAsync(
        string requesterDiscordId,
        string claimerDiscordId,
        string claimerDisplayName,
        string requesterDisplayName,
        Guid requestId,
        string requestTitle,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            RequestId = requestId,
            RequestType = "Assistance",
            RequestLabel = requestTitle,
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
