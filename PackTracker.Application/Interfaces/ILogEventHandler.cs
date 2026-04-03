using System.Text.RegularExpressions;
using PackTracker.Domain.Events;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Interface for log event handlers that parse Star Citizen game log entries.
/// </summary>
/// <remarks>
/// Implementing classes should:
/// 1. Define a compiled Regex pattern for matching specific log events
/// 2. Implement Handle() to extract data and fire appropriate events via PackTrackerEventDispatcher
/// 3. Optionally set a Priority for handler execution order (lower = higher priority)
///
/// Handler Pattern Benefits:
/// - Single Responsibility: Each handler focuses on one event type
/// - Extensibility: Add new event types by creating new handlers
/// - Testability: Handlers can be unit tested in isolation
/// - Performance: Compiled regex patterns are cached
/// </remarks>
public interface ILogEventHandler
{
    /// <summary>
    /// Compiled regex pattern for matching this handler's log event type.
    /// </summary>
    /// <remarks>
    /// Should use RegexOptions.Compiled for performance.
    /// Pattern should use named capture groups for clarity.
    /// </remarks>
    Regex Pattern { get; }

    /// <summary>
    /// Process a matched log entry and fire appropriate events.
    /// </summary>
    /// <param name="entry">The log entry to process</param>
    /// <remarks>
    /// Implementation should:
    /// 1. Match the Pattern against entry.Message
    /// 2. Extract data from named capture groups
    /// 3. Create appropriate event data record
    /// 4. Fire event via PackTrackerEventDispatcher.OnXxxEvent()
    /// </remarks>
    void Handle(LogEntry entry);

    /// <summary>
    /// Priority for handler execution (lower = higher priority).
    /// </summary>
    /// <remarks>
    /// Default is 100. Use lower values for handlers that should execute first.
    /// Useful when multiple handlers might match the same log line.
    /// </remarks>
    int Priority => 100;
}
