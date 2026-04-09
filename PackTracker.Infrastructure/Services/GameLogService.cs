using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Monitors a Star Citizen game log file and emits raw log lines for downstream feature parsers.
/// This service is intentionally generic and does not contain kill-tracking-specific logic.
/// </summary>
public class GameLogService : IGameLogService
{
    #region Fields

    private readonly ILogger<GameLogService> _logger;
    private long _lastPosition;
    private CancellationTokenSource? _internalCancellationTokenSource;

    #endregion

    #region Events

    /// <inheritdoc />
    public event Action? Connected;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public event Action<string>? LineReceived;

    #endregion

    #region Properties

    /// <inheritdoc />
    public bool IsMonitoring { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="GameLogService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for structured log output.</param>
    public GameLogService(ILogger<GameLogService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task StartAsync(string logFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
            throw new ArgumentException("A valid game log file path is required.", nameof(logFilePath));

        if (!File.Exists(logFilePath))
        {
            _logger.LogWarning("Game log file not found. Path={Path}", logFilePath);
            return;
        }

        if (IsMonitoring)
        {
            _logger.LogInformation("Game log monitor is already running. Path={Path}", logFilePath);
            return;
        }

        _internalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _internalCancellationTokenSource.Token;

        IsMonitoring = true;

        _logger.LogInformation("Starting game log monitor. Path={Path}", logFilePath);

        await using var stream = new FileStream(
            logFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        using var reader = new StreamReader(stream);

        if (_lastPosition > stream.Length)
        {
            _logger.LogInformation(
                "Detected log truncation or rollover. Resetting read position. PreviousPosition={PreviousPosition} CurrentLength={CurrentLength}",
                _lastPosition,
                stream.Length);

            _lastPosition = 0;
        }

        stream.Seek(_lastPosition, SeekOrigin.Begin);

        Connected?.Invoke();

        try
        {
            while (!linkedToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();

                if (line is null)
                {
                    _lastPosition = stream.Position;
                    await Task.Delay(500, linkedToken);
                    continue;
                }

                _lastPosition = stream.Position;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    LineReceived?.Invoke(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Game log monitoring cancelled normally.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while monitoring the game log. Path={Path}", logFilePath);
            throw;
        }
        finally
        {
            IsMonitoring = false;
            Disconnected?.Invoke();
            _logger.LogInformation("Stopped game log monitor. Path={Path}", logFilePath);
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        if (!IsMonitoring)
            return Task.CompletedTask;

        _logger.LogInformation("Stop requested for game log monitor.");

        _internalCancellationTokenSource?.Cancel();
        return Task.CompletedTask;
    }

    #endregion
}