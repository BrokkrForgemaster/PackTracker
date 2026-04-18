namespace PackTracker.Application.Interfaces;

public interface ICraftingWorkflowNotifier
{
    Task NotifyAsync(string eventName, Guid requestId, CancellationToken cancellationToken);
}
