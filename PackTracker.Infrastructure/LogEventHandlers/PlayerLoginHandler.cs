using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Events;
using PackTracker.Infrastructure.Events;

namespace PackTracker.Infrastructure.LogEventHandlers;

/// <summary>
/// Handles Player Login log events from Star Citizen game.log.
/// Parses player login information and fires PlayerLoginEvent.
/// </summary>
public class PlayerLoginHandler : ILogEventHandler
{
    private readonly ILogger<PlayerLoginHandler> _logger;

    /// <summary>
    /// Regex pattern for Star Citizen Player Login log entries.
    /// </summary>
    /// <remarks>
    /// Example log line:
    /// &lt;Notice&gt; [UserService] Player login: PlayerUsername
    /// </remarks>
    public Regex Pattern { get; } = new Regex(
        @"<Notice> \[UserService\] Player login: (?<Username>.+)",
        RegexOptions.Compiled
    );

    public int Priority => 10; // High priority - login events are important

    public PlayerLoginHandler(ILogger<PlayerLoginHandler> logger)
    {
        _logger = logger;
    }

    public void Handle(LogEntry entry)
    {
        var match = Pattern.Match(entry.Message);
        if (!match.Success)
            return;

        try
        {
            var username = match.Groups["Username"].Value.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("Player login detected but username is empty");
                return;
            }

            PackTrackerEventDispatcher.OnPlayerLoginEvent(username);

            _logger.LogInformation("Player logged in: {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process player login event");
        }
    }
}
