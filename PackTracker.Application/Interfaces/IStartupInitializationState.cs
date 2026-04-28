namespace PackTracker.Application.Interfaces;

public interface IStartupInitializationState
{
    bool IsInitialized { get; }
    string? FailureMessage { get; }
    DateTimeOffset? CompletedAtUtc { get; }

    void MarkSucceeded();
    void MarkFailed(string failureMessage);
}
