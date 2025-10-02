using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;
/// <summary>
/// Defines a game-log service that tails the log file and pushes parsed events.
/// </summary>
public interface IGameLogService
{
    event Action<KillEntity> KillParsed;
    Task StartAsync(string logFilePath, CancellationToken cancellationToken);
    Task StopAsync();
}