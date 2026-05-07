using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PackTracker.Application.Interfaces;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Thread-safe singleton implementation of <see cref="IUpdateNotificationState"/>.
/// Property change notifications are raised on the calling thread; UI bindings should
/// marshal to the dispatcher if they live on the UI thread.
/// </summary>
public sealed class UpdateNotificationState : IUpdateNotificationState
{
    private readonly object _gate = new();

    private UpdateLifecycleStatus _status = UpdateLifecycleStatus.Idle;
    private UpdateInfo? _availableUpdate;
    private int _downloadProgress;
    private string? _stagedInstallerPath;
    private string? _failureMessage;
    private string? _skippedVersion;
    private DateTimeOffset? _remindLaterUntilUtc;

    public event PropertyChangedEventHandler? PropertyChanged;

    public UpdateLifecycleStatus Status
    {
        get { lock (_gate) return _status; }
        private set => SetField(ref _status, value);
    }

    public UpdateInfo? AvailableUpdate
    {
        get { lock (_gate) return _availableUpdate; }
        private set => SetField(ref _availableUpdate, value);
    }

    public int DownloadProgress
    {
        get { lock (_gate) return _downloadProgress; }
        private set => SetField(ref _downloadProgress, value);
    }

    public string? StagedInstallerPath
    {
        get { lock (_gate) return _stagedInstallerPath; }
        private set => SetField(ref _stagedInstallerPath, value);
    }

    public string? FailureMessage
    {
        get { lock (_gate) return _failureMessage; }
        private set => SetField(ref _failureMessage, value);
    }

    public string? SkippedVersion
    {
        get { lock (_gate) return _skippedVersion; }
        private set => SetField(ref _skippedVersion, value);
    }

    public DateTimeOffset? RemindLaterUntilUtc
    {
        get { lock (_gate) return _remindLaterUntilUtc; }
        private set => SetField(ref _remindLaterUntilUtc, value);
    }

    public void ReportChecking()
    {
        // Don't regress out of a staged update just because the next poll is starting.
        if (Status is UpdateLifecycleStatus.ReadyToInstall) return;

        Status = UpdateLifecycleStatus.Checking;
        FailureMessage = null;
    }

    public void ReportUpdateAvailable(UpdateInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (Status is UpdateLifecycleStatus.ReadyToInstall) return;

        AvailableUpdate = info;
        DownloadProgress = 0;
        StagedInstallerPath = null;
        FailureMessage = null;
        Status = UpdateLifecycleStatus.UpdateAvailable;
    }

    public void ReportDownloading(int percent)
    {
        // IProgress<int>.Report queues onto the threadpool, so a final 100% callback can
        // race with ReportReadyToInstall. Refuse to regress once the install is staged.
        if (Status is UpdateLifecycleStatus.ReadyToInstall) return;

        DownloadProgress = Math.Clamp(percent, 0, 100);
        Status = UpdateLifecycleStatus.Downloading;
    }

    public void ReportReadyToInstall(UpdateInfo info, string installerPath)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);
        AvailableUpdate = info;
        StagedInstallerPath = installerPath;
        DownloadProgress = 100;
        FailureMessage = null;
        Status = UpdateLifecycleStatus.ReadyToInstall;
    }

    private bool _hasShownUpdatedThisSession;

    public void ReportFailed(string message)
    {
        FailureMessage = message;
        Status = UpdateLifecycleStatus.Failed;
    }

    public void ReportUpdated(string version)
    {
        if (_hasShownUpdatedThisSession) return;

        AvailableUpdate = new UpdateInfo 
        { 
            Version = version,
            DownloadUrl = "about:blank" // Required property, though unused in 'Updated' state
        };
        Status = UpdateLifecycleStatus.Updated;
        _hasShownUpdatedThisSession = true;
    }

    public void ReportIdle()
    {
        // Don't clear "Updated" status automatically if it was just set.
        if (Status == UpdateLifecycleStatus.Updated) return;

        AvailableUpdate = null;
        StagedInstallerPath = null;
        DownloadProgress = 0;
        FailureMessage = null;
        Status = UpdateLifecycleStatus.Idle;
    }

    public void RemindLater(TimeSpan delay)
    {
        RemindLaterUntilUtc = DateTimeOffset.UtcNow.Add(delay);
        Status = UpdateLifecycleStatus.Idle;
    }

    public void SkipVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        SkippedVersion = version.TrimStart('v');
        AvailableUpdate = null;
        StagedInstallerPath = null;
        Status = UpdateLifecycleStatus.Idle;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        bool changed;
        lock (_gate)
        {
            if (Equals(field, value))
            {
                return;
            }
            field = value;
            changed = true;
        }

        if (changed)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
