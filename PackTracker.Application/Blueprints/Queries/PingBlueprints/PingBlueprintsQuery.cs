using MediatR;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Blueprints.Queries.PingBlueprints;

public sealed record PingBlueprintsQuery : IRequest<BlueprintPingResult>;

public sealed record BlueprintPingResult(
    bool CanConnect,
    string? Provider,
    IReadOnlyList<string> PendingMigrations,
    int AppliedMigrationsCount,
    DateTimeOffset Timestamp);

public sealed class PingBlueprintsQueryHandler : IRequestHandler<PingBlueprintsQuery, BlueprintPingResult>
{
    private readonly IDatabaseDiagnostics _diagnostics;

    public PingBlueprintsQueryHandler(IDatabaseDiagnostics diagnostics) => _diagnostics = diagnostics;

    public async Task<BlueprintPingResult> Handle(PingBlueprintsQuery request, CancellationToken cancellationToken)
    {
        var canConnect = await _diagnostics.CanConnectAsync(cancellationToken);
        var pending = await _diagnostics.GetPendingMigrationsAsync(cancellationToken);
        var applied = await _diagnostics.GetAppliedMigrationsCountAsync(cancellationToken);

        return new BlueprintPingResult(canConnect, _diagnostics.ProviderName, pending, applied, DateTimeOffset.UtcNow);
    }
}
