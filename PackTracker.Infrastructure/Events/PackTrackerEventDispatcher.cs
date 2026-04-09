using PackTracker.Domain.Events;

namespace PackTracker.Infrastructure.Events;

/// <summary>
/// Central event bus for all PackTracker game events using the Observer pattern.
/// </summary>
/// <remarks>
/// This static class decouples event producers (log handlers) from consumers (UI, services, managers).
///
/// Architecture:
/// - GameLogService parses log entries and calls OnEventName() methods
/// - Registered subscribers (via += operator) receive notifications
/// - Events are broadcast synchronously on the calling thread
/// - Thread safety: Relies on C# event thread-safety guarantees
///
/// Typical Usage Pattern:
/// <code>
/// // Subscribe (usually in constructor)
/// PackTrackerEventDispatcher.ActorDeathEvent += HandleKill;
///
/// // Unsubscribe (usually in Dispose/cleanup)
/// PackTrackerEventDispatcher.ActorDeathEvent -= HandleKill;
///
/// // Publish (from LogEventHandler)
/// PackTrackerEventDispatcher.OnActorDeathEvent(killData);
/// </code>
/// </remarks>
public static class PackTrackerEventDispatcher
{
    #region Player State Events

    /// <summary>
    /// Fired when the local player successfully logs into Star Citizen.
    /// </summary>
    public static event Action<string>? PlayerLoginEvent;

    /// <summary>
    /// Publishes a player login event to all subscribers.
    /// </summary>
    /// <param name="username">Player's username (handle)</param>
    public static void OnPlayerLoginEvent(string username)
    {
        PlayerLoginEvent?.Invoke(username);
    }

    #endregion

    #region Game Mode Events

    /// <summary>
    /// Fired when the player changes game mode (Arena Commander vs Persistent Universe).
    /// </summary>
    public static event Action<GameMode>? GameModeChangedEvent;

    /// <summary>
    /// Publishes a game mode change event to all subscribers.
    /// </summary>
    public static void OnGameModeChangedEvent(GameMode mode)
    {
        GameModeChangedEvent?.Invoke(mode);
    }

    #endregion

    #region Location Events

    /// <summary>
    /// Fired when the player's location changes significantly.
    /// </summary>
    public static event Action<string>? LocationChangedEvent;

    /// <summary>
    /// Publishes a location change event to all subscribers.
    /// </summary>
    public static void OnLocationChangedEvent(string location)
    {
        LocationChangedEvent?.Invoke(location);
    }

    #endregion

    #region Connection Events

    /// <summary>
    /// Fired when the log monitoring connection status changes.
    /// </summary>
    public static event Action<ConnectionStatus>? ConnectionStatusChanged;

    /// <summary>
    /// Publishes a connection status change event to all subscribers.
    /// </summary>
    public static void OnConnectionStatusChanged(ConnectionStatus status)
    {
        ConnectionStatusChanged?.Invoke(status);
    }

    #endregion
}
