using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically removes expired login states and refresh tokens to prevent database bloat.
/// Also clears stale distributed locks.
/// </summary>
public sealed class TokenCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TokenCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

    public TokenCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<TokenCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Token Cleanup Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during token cleanup.");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Token Cleanup Background Service is stopping.");
    }

    private async Task DoCleanupAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // 1. Cleanup expired LoginStates
        var expiredLoginStates = await db.LoginStates
            .Where(s => s.CreatedAt < now.AddMinutes(-10))
            .ToListAsync(ct);

        if (expiredLoginStates.Count > 0)
        {
            db.LoginStates.RemoveRange(expiredLoginStates);
            _logger.LogInformation("Removed {Count} expired login states.", expiredLoginStates.Count);
        }

        // 2. Cleanup expired/revoked RefreshTokens
        // We keep revoked tokens for a few days for audit/forensics, then purge.
        var expiredTokens = await db.RefreshTokens
            .Where(rt => rt.ExpiresAt < now || (rt.IsRevoked && rt.RevokedAt < now.AddDays(-7)))
            .ToListAsync(ct);

        if (expiredTokens.Count > 0)
        {
            db.RefreshTokens.RemoveRange(expiredTokens);
            _logger.LogInformation("Removed {Count} expired or old revoked refresh tokens.", expiredTokens.Count);
        }

        // 3. Cleanup stale DistributedLocks (safeguard)
        var staleLocks = await db.DistributedLocks
            .Where(l => l.ExpiresAt < now.AddMinutes(-60)) // Grace period
            .ToListAsync(ct);

        if (staleLocks.Count > 0)
        {
            db.DistributedLocks.RemoveRange(staleLocks);
            _logger.LogInformation("Removed {Count} stale distributed locks.", staleLocks.Count);
        }

        if (expiredLoginStates.Count > 0 || expiredTokens.Count > 0 || staleLocks.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
