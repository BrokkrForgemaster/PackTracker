using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// GameLogService monitors a game log file for new kill events, parses them,
/// and raises KillParsed events as new entries are detected.
/// </summary>
public class GameLogService : IGameLogService
{
    #region Fields

    private readonly ILogger<GameLogService> _logger;
    private long _lastPosition;
    public event Action<KillEntity>? KillParsed;

    #endregion

    #region Constructor

    public GameLogService(ILogger<GameLogService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static bool IsReady { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Starts monitoring the specified log file for new kill events.
    /// Raises KillParsed for each new entry detected.
    /// </summary>
    /// <param name="logFilePath">The path to the game log file.</param>
    /// <param name="cancellationToken">A cancellation token to stop monitoring.</param>
    public async Task StartAsync(string logFilePath, CancellationToken cancellationToken)
    {
        // Diagnostic logging to verify file access
        if (!File.Exists(logFilePath))
        {
            _logger.LogError("Game log file not found at path: {Path}", logFilePath);
            KillParsed?.Invoke(new KillEntity { Type = "Info", Summary = "No log file", Timestamp = DateTime.Now });
            return;
        }

        _logger.LogInformation("Starting GameLogService for {Path}", logFilePath);

        // Raise Connected event only after confirming file exists and can be opened
        KillParsed?.Invoke(new KillEntity { Type = "Info", Summary = "Connected", Timestamp = DateTime.Now });

        await using var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        stream.Seek(_lastPosition, SeekOrigin.Begin);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                _lastPosition = stream.Position;

                // Replace this with your actual kill parsing logic
                var kill = KillParser.ExtractKill(line); // You must implement KillParser.ExtractKill
                if (kill == null) continue;

                KillParsed?.Invoke(kill);
            }
        }
        finally
        {
            KillParsed?.Invoke(new KillEntity { Type = "Info", Summary = "Disconnected", Timestamp = DateTime.Now });
            _logger.LogInformation("Stopped GameLogService for {Path}", logFilePath);
        }
    }

    public Task StopAsync() => Task.CompletedTask;

    #endregion
}
