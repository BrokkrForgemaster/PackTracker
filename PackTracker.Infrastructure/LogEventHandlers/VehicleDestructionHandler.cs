using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Events;
using PackTracker.Infrastructure.Events;

namespace PackTracker.Infrastructure.LogEventHandlers;

/// <summary>
/// Handles Vehicle Destruction log events from Star Citizen game.log.
/// Parses vehicle destruction information and fires VehicleDestructionEvent.
/// </summary>
public class VehicleDestructionHandler : ILogEventHandler
{
    private readonly ILogger<VehicleDestructionHandler> _logger;

    /// <summary>
    /// Regex pattern for Star Citizen Vehicle Destruction log entries.
    /// </summary>
    /// <remarks>
    /// Example log line:
    /// &lt;2024-12-01T12:00:00.000Z&gt; [Notice] &lt;Vehicle&gt; CVehicle::OnDestroyed: 'AEGS_Gladius_123'
    /// </remarks>
    public Regex Pattern { get; } = new Regex(
        @"<(?<Timestamp>\d{4}-\d{2}-\d{2}T[^>]+)>.*<Vehicle> CVehicle::OnDestroyed: '(?<VehicleName>[^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public int Priority => 60;

    public VehicleDestructionHandler(ILogger<VehicleDestructionHandler> logger)
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
            var vehicleName = match.Groups["VehicleName"].Value.Trim();
            var timestampStr = match.Groups["Timestamp"].Value;

            if (!DateTime.TryParse(timestampStr, out var timestamp))
            {
                _logger.LogWarning("Failed to parse timestamp: {Timestamp}", timestampStr);
                timestamp = DateTime.UtcNow;
            }

            var data = new VehicleDestructionData(
                VehicleName: vehicleName,
                Owner: "Unknown", // Owner info not in log line
                Timestamp: timestamp,
                Location: "Unknown"
            );

            PackTrackerEventDispatcher.OnVehicleDestructionEvent(data);

            _logger.LogDebug("Vehicle destruction processed: {Vehicle}", vehicleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process vehicle destruction event");
        }
    }
}
