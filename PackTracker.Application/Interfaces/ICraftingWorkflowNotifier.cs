namespace PackTracker.Application.Interfaces;

public interface ICraftingWorkflowNotifier
{
    Task NotifyAsync(string eventName, Guid requestId, CancellationToken cancellationToken);

    Task NotifyClaimedAsync(
        string requesterDiscordId,
        string claimerDiscordId,
        string claimerDisplayName,
        string requesterDisplayName,
        Guid requestId,
        string requestType,
        string requestLabel,
        CancellationToken cancellationToken);
}
