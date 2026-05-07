using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;

namespace PackTracker.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically polls GitHub Releases for a newer version, populates the shared
/// <see cref="IUpdateNotificationState"/>, and (when configured) downloads the
/// installer in the background so the user can choose when to apply it.
/// </summary>
public sealed class UpdateMonitorBackgroundService : BackgroundService
{
    private readonly IUpdateService _updateService;
    private readonly IUpdateNotificationState _state;
    private readonly UpdateOptions _options;
    private readonly ILogger<UpdateMonitorBackgroundService> _logger;

    public UpdateMonitorBackgroundService(
        IUpdateService updateService,
        IUpdateNotificationState state,
        IOptions<UpdateOptions> options,
        ILogger<UpdateMonitorBackgroundService> logger)
    {
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoCheckEnabled)
        {
            _logger.LogInformation("Update auto-check disabled by configuration.");
            return;
        }

        var initialDelay = TimeSpan.FromSeconds(Math.Max(0, _options.InitialDelaySeconds));
        var pollInterval = TimeSpan.FromHours(Math.Max(0.25, _options.CheckIntervalHours));

        _logger.LogInformation(
            "Update monitor starting. InitialDelay={InitialDelay} PollInterval={PollInterval}",
            initialDelay,
            pollInterval);

        try
        {
            await Task.Delay(initialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update monitor poll failed.");
                _state.ReportFailed(ex.Message);
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Update monitor stopping.");
    }

    private async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        // If the user clicked "Remind me later", skip polling until that window has passed.
        if (_state.RemindLaterUntilUtc is { } until && DateTimeOffset.UtcNow < until)
        {
            return;
        }

        // Once an update is staged and waiting for the user, don't keep re-checking it.
        if (_state.Status == UpdateLifecycleStatus.ReadyToInstall)
        {
            return;
        }

        var currentVersion = _updateService.GetCurrentVersion();
        
        // Check for recent successful update via bootstrap log
        var logPath = Path.Combine(Path.GetTempPath(), "PackTracker", "installer-bootstrap.log");
        if (File.Exists(logPath))
        {
            try
            {
                var logContent = await File.ReadAllTextAsync(logPath, cancellationToken);
                // Simple heuristic: if log exists and last check was today, and version is current.
                // We'll just show it once per session if the version matches.
                if (logContent.Contains("powershell finished with exit code 0", StringComparison.OrdinalIgnoreCase))
                {
                    // If we've already shown "Updated" this session, don't keep doing it.
                    // But we'll let the service decide based on state.
                }
            }
            catch { /* Ignore log read errors */ }
        }

        _state.ReportChecking();

        var info = await _updateService.CheckForUpdateAsync(cancellationToken);

        if (info is null)
        {
            // If we are at the "latest" version, and we haven't shown "Updated" yet,
            // we can potentially show a "You are on the latest version" if triggered manually,
            // but for background polling, we'll just go idle.
            _state.ReportIdle();
            return;
        }

        // If the version we just found is the same as current, we might have just updated.
        if (string.Equals(currentVersion, info.Version, StringComparison.OrdinalIgnoreCase))
        {
            _state.ReportUpdated(info.Version);
            return;
        }

        if (string.Equals(_state.SkippedVersion, info.Version, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping version {Version} per user preference.", info.Version);
            _state.ReportIdle();
            return;
        }

        _state.ReportUpdateAvailable(info);

        if (!_options.AutoDownload)
        {
            return;
        }

        try
        {
            var progress = new Progress<int>(p => _state.ReportDownloading(p));
            var path = await _updateService.DownloadUpdateAsync(info, progress, cancellationToken);
            _state.ReportReadyToInstall(info, path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background download failed for version {Version}.", info.Version);
            _state.ReportFailed(ex.Message);
        }
    }
}
