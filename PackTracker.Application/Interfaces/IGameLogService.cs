namespace PackTracker.Application.Interfaces;

/// <summary>
/// Defines a generic service for monitoring a Star Citizen game log file
/// and emitting raw log lines for higher-level feature parsers.
/// </summary>
public interface IGameLogService
{
    #region Events

    /// <summary>
    /// Raised when the log monitor connects to the target log file.
    /// </summary>
    event Action? Connected;

    /// <summary>
    /// Raised when the log monitor disconnects from the target log file.
    /// </summary>
    event Action? Disconnected;

    /// <summary>
    /// Raised when a new raw log line is read from the game log.
    /// </summary>
    event Action<string>? LineReceived;

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the service is actively monitoring the log.
    /// </summary>
    bool IsMonitoring { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Starts monitoring the specified game log file for new lines.
    /// </summary>
    /// <param name="logFilePath">The full path to the game log file.</param>
    /// <param name="cancellationToken">A cancellation token used to stop monitoring.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync(string logFilePath, CancellationToken cancellationToken);

    /// <summary>
    /// Stops the log monitoring service.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync();

    #endregion
}