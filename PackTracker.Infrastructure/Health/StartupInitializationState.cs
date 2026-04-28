using PackTracker.Application.Interfaces;

namespace PackTracker.Infrastructure.Health;

public sealed class StartupInitializationState : IStartupInitializationState
{
    private readonly object _sync = new();

    public bool IsInitialized { get; private set; }
    public string? FailureMessage { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void MarkSucceeded()
    {
        lock (_sync)
        {
            IsInitialized = true;
            FailureMessage = null;
            CompletedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkFailed(string failureMessage)
    {
        lock (_sync)
        {
            IsInitialized = false;
            FailureMessage = string.IsNullOrWhiteSpace(failureMessage)
                ? "Unknown startup initialization failure."
                : failureMessage;
            CompletedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}
