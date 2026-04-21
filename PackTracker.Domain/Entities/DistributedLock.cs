namespace PackTracker.Domain.Entities;

/// <summary>
/// Simple database-backed distributed lock to prevent concurrent execution
/// of critical tasks across multiple server instances.
/// </summary>
public sealed class DistributedLock
{
    /// <summary>
    /// The unique name/key for the lock.
    /// </summary>
    public string LockKey { get; set; } = string.Empty;

    /// <summary>
    /// Identifier for the server instance currently holding the lock.
    /// </summary>
    public string LockedBy { get; set; } = string.Empty;

    public DateTime LockedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// After this time, the lock is considered stale and can be overtaken.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
