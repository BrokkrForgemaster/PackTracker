using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Services;

public sealed class DatabaseDistributedLockService : IDistributedLockService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseDistributedLockService> _logger;

    public DatabaseDistributedLockService(AppDbContext db, ILogger<DatabaseDistributedLockService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> AcquireLockAsync(string lockKey, string owner, TimeSpan expiration, CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            var lockEntry = await _db.DistributedLocks
                .FirstOrDefaultAsync(l => l.LockKey == lockKey, ct);

            if (lockEntry == null)
            {
                // Try to create new lock
                _db.DistributedLocks.Add(new DistributedLock
                {
                    LockKey = lockKey,
                    LockedBy = owner,
                    LockedAt = now,
                    ExpiresAt = now.Add(expiration)
                });
            }
            else if (lockEntry.ExpiresAt < now)
            {
                // Take over expired lock
                lockEntry.LockedBy = owner;
                lockEntry.LockedAt = now;
                lockEntry.ExpiresAt = now.Add(expiration);
            }
            else
            {
                // Active lock held by someone else
                return false;
            }

            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Concurrent insert or update failed
            return false;
        }
    }

    public async Task ReleaseLockAsync(string lockKey, string owner, CancellationToken ct)
    {
        var lockEntry = await _db.DistributedLocks
            .FirstOrDefaultAsync(l => l.LockKey == lockKey && l.LockedBy == owner, ct);

        if (lockEntry != null)
        {
            _db.DistributedLocks.Remove(lockEntry);
            await _db.SaveChangesAsync(ct);
        }
    }
}
