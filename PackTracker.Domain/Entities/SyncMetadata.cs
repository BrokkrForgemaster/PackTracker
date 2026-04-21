namespace PackTracker.Domain.Entities;

/// <summary>
/// Records the status and timing of various synchronization tasks.
/// Replaces volatile static fields to ensure consistency across server instances.
/// </summary>
public sealed class SyncMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Unique name for the sync task (e.g., "WikiBlueprints", "WikiItems", "UexCommodities").
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    public DateTime? LastStartedAt { get; set; }
    public DateTime? LastCompletedAt { get; set; }
    
    public bool IsSuccess { get; set; }
    public string? LastErrorMessage { get; set; }
    
    public int ItemsProcessed { get; set; }
}
