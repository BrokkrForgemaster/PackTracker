namespace PackTracker.Application.Interfaces;

public interface IDistributedLockService
{
    /// <summary>
    /// Attempts to acquire a distributed lock.
    /// </summary>
    /// <param name="lockKey">Unique name of the lock.</param>
    /// <param name="owner">Identifier of the requester (e.g., machine name).</param>
    /// <param name="expiration">How long the lock is valid for.</param>
    /// <returns>True if the lock was acquired; otherwise false.</returns>
    Task<bool> AcquireLockAsync(string lockKey, string owner, TimeSpan expiration, CancellationToken ct);

    /// <summary>
    /// Releases a held lock.
    /// </summary>
    Task ReleaseLockAsync(string lockKey, string owner, CancellationToken ct);
}
