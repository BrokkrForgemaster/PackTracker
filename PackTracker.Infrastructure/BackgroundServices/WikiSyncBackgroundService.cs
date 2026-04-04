using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.BackgroundServices;

public sealed class WikiSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WikiSyncBackgroundService> _logger;

    public WikiSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<WikiSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for API to finish initializing
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var blueprintCount = await db.Blueprints.CountAsync(stoppingToken);

            if (blueprintCount >= 10)
            {
                _logger.LogInformation("Wiki auto-sync skipped: {Count} blueprints already in database.", blueprintCount);
                return;
            }

            _logger.LogInformation("Wiki auto-sync starting: only {Count} blueprints found, pulling from Star Citizen Wiki...", blueprintCount);

            var wikiSync = scope.ServiceProvider.GetRequiredService<IWikiSyncService>();
            var result = await wikiSync.SyncBlueprintsAsync(stoppingToken);

            _logger.LogInformation(
                "Wiki auto-sync complete: created={Created}, updated={Updated}, failed={Failed}{Error}",
                result.Created, result.Updated, result.Failed,
                result.ErrorMessage != null ? $", error={result.ErrorMessage}" : string.Empty);
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested — normal path
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki auto-sync background service encountered an unexpected error");
        }
    }
}
