namespace PackTracker.Application.Interfaces;

public interface IAssistanceRequestNotifier
{
    Task NotifyCreatedAsync(Guid requestId, CancellationToken cancellationToken);

    Task NotifyUpdatedAsync(Guid requestId, CancellationToken cancellationToken);
}
