using PackTracker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Services;

/// <summary name="SignalRCraftingWorkflowNotifier">
/// Implements <see cref="ICraftingWorkflowNotifier"/> using SignalR to send real-time notifications to connected clients.
/// </summary>
public sealed class SignalRCraftingWorkflowNotifier : ICraftingWorkflowNotifier
{
    #region Properties
    private readonly IHubContext<RequestsHub> _hubContext;
    private readonly IDiscordAnnouncementService _discord;
    #endregion
    
    #region Constructor
    public SignalRCraftingWorkflowNotifier(IHubContext<RequestsHub> hubContext, IDiscordAnnouncementService discord)
    {
        _hubContext = hubContext;
        _discord = discord;
    }
    #endregion
    
    #region Methods
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

        await _discord.NotifyRequestClaimedAsync(
            requestType: requestType,
            requestLabel: requestLabel,
            requesterDisplayName: requesterDisplayName,
            claimerDisplayName: claimerDisplayName,
            requestId: requestId);
    }
    #endregion
}
