using Microsoft.Extensions.Diagnostics.HealthChecks;
using PackTracker.Application.Interfaces;

namespace PackTracker.Infrastructure.Health;

public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    private readonly IDatabaseDiagnostics _diagnostics;
    private readonly IStartupInitializationState _startupState;

    public DatabaseReadinessHealthCheck(
        IDatabaseDiagnostics diagnostics,
        IStartupInitializationState startupState)
    {
        _diagnostics = diagnostics;
        _startupState = startupState;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_startupState.IsInitialized)
        {
            return HealthCheckResult.Unhealthy(
                description: _startupState.FailureMessage ?? "Application startup initialization has not completed successfully.",
                data: BuildData(canConnect: null, pendingMigrations: null));
        }

        var canConnect = await _diagnostics.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return HealthCheckResult.Unhealthy(
                description: "Database connection failed.",
                data: BuildData(canConnect, pendingMigrations: null));
        }

        var pendingMigrations = await _diagnostics.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Count > 0)
        {
            return HealthCheckResult.Unhealthy(
                description: "Pending database migrations detected.",
                data: BuildData(canConnect, pendingMigrations));
        }

        return HealthCheckResult.Healthy(
            description: "Database is reachable and schema is up to date.",
            data: BuildData(canConnect, pendingMigrations));
    }

    private Dictionary<string, object> BuildData(bool? canConnect, IReadOnlyList<string>? pendingMigrations)
    {
        return new Dictionary<string, object>
        {
            ["provider"] = _diagnostics.ProviderName ?? "unknown",
            ["canConnect"] = canConnect?.ToString() ?? "unknown",
            ["pendingMigrations"] = pendingMigrations?.ToArray() ?? [],
            ["startupCompletedAtUtc"] = _startupState.CompletedAtUtc?.ToString("O") ?? string.Empty
        };
    }
}
