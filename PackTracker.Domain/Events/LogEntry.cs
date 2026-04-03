namespace PackTracker.Domain.Events;

/// <summary>
/// Represents a single parsed log entry from Star Citizen's game.log.
/// </summary>
/// <remarks>
/// This lightweight DTO is passed to all registered event handlers for processing.
/// </remarks>
public class LogEntry
{
    /// <summary>Timestamp when the log entry was captured (current system time)</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Raw log line from game.log</summary>
    public required string Message { get; set; }
}
