namespace PackTracker.Application.Interfaces;

public interface IAssistanceRequestNotifier
{
    Task NotifyCreatedAsync(Guid requestId, CancellationToken cancellationToken);

    Task NotifyUpdatedAsync(Guid requestId, CancellationToken cancellationToken);

    Task NotifyClaimedAsync(
        string requesterDiscordId,
        string claimerDiscordId,
        string claimerDisplayName,
        string requesterDisplayName,
        Guid requestId,
        string requestTitle,
        CancellationToken cancellationToken);
}
