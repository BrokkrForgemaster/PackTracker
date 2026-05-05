using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically prunes old audit logs to prevent database bloat.
/// Default retention is 30 days.
/// </summary>
public sealed class AuditLogCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogCleanupBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);
    private readonly int _retentionDays = 30;

    public AuditLogCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AuditLogCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit Log Cleanup Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PruneLogsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during audit log pruning.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Audit Log Cleanup Background Service is stopping.");
    }

    private async Task PruneLogsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

        // We use ExecuteDeleteAsync for efficiency on large datasets if supported,
        // otherwise fallback to traditional removal.
        int deletedCount = await db.AdminAuditLogs
            .Where(x => x.OccurredAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Pruned {Count} audit logs older than {Days} days.", deletedCount, _retentionDays);
        }
    }
}
