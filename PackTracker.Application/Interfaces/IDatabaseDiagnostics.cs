namespace PackTracker.Application.Interfaces;

public interface IDatabaseDiagnostics
{
    string? ProviderName { get; }
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken);
    Task<int> GetAppliedMigrationsCountAsync(CancellationToken cancellationToken);
}
