namespace PackTracker.Domain.Events;

/// <summary>
/// Represents the connection status of the game log monitoring service.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>Not connected to game log</summary>
    Disconnected,

    /// <summary>Attempting to connect to game log</summary>
    Connecting,

    /// <summary>Successfully connected and monitoring</summary>
    Connected,

    /// <summary>Error occurred during connection or monitoring</summary>
    Error
}
