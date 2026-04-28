using MediatR;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Blueprints.Queries.PingBlueprints;

public sealed record PingBlueprintsQuery : IRequest<BlueprintPingResult>;

public sealed record BlueprintPingResult(
    bool CanConnect,
    string? Provider,
    IReadOnlyList<string> PendingMigrations,
    int AppliedMigrationsCount,
    string? DiagnosticsErrorMessage,
    bool IsStartupInitialized,
    string? StartupFailureMessage,
    DateTimeOffset? StartupCompletedAtUtc,
    DateTimeOffset Timestamp);

public sealed class PingBlueprintsQueryHandler : IRequestHandler<PingBlueprintsQuery, BlueprintPingResult>
{
    private readonly IDatabaseDiagnostics _diagnostics;
    private readonly IStartupInitializationState _startupState;

    public PingBlueprintsQueryHandler(
        IDatabaseDiagnostics diagnostics,
        IStartupInitializationState startupState)
    {
        _diagnostics = diagnostics;
        _startupState = startupState;
    }

    public async Task<BlueprintPingResult> Handle(PingBlueprintsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _diagnostics.CanConnectAsync(cancellationToken);
            var pending = await _diagnostics.GetPendingMigrationsAsync(cancellationToken);
            var applied = await _diagnostics.GetAppliedMigrationsCountAsync(cancellationToken);

            return new BlueprintPingResult(
                canConnect,
                _diagnostics.ProviderName,
                pending,
                applied,
                null,
                _startupState.IsInitialized,
                _startupState.FailureMessage,
                _startupState.CompletedAtUtc,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new BlueprintPingResult(
                false,
                _diagnostics.ProviderName,
                [],
                0,
                ex.Message,
                _startupState.IsInitialized,
                _startupState.FailureMessage,
                _startupState.CompletedAtUtc,
                DateTimeOffset.UtcNow);
        }
    }
}
