using Microsoft.AspNetCore.SignalR;
using PackTracker.Api.Hubs;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Services;

public sealed class SignalRAssistanceRequestNotifier : IAssistanceRequestNotifier
{
    private readonly IHubContext<RequestsHub> _hubContext;
    private readonly IDiscordNotifier _discord;

    public SignalRAssistanceRequestNotifier(IHubContext<RequestsHub> hubContext, IDiscordNotifier discord)
    {
        _hubContext = hubContext;
        _discord = discord;
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

        await _discord.NotifyRequestClaimedAsync(
            requestType: "Assistance",
            requestLabel: requestTitle,
            requesterDisplayName: requesterDisplayName,
            claimerDisplayName: claimerDisplayName,
            requestId: requestId);
    }
}
