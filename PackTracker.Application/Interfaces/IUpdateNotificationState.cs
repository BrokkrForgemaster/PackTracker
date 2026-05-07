using System;
using System.ComponentModel;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Observable singleton that tracks the current update lifecycle so the shell
/// UI can react without polling the update service directly.
/// </summary>
public interface IUpdateNotificationState : INotifyPropertyChanged
{
    UpdateLifecycleStatus Status { get; }
    UpdateInfo? AvailableUpdate { get; }
    int DownloadProgress { get; }
    string? StagedInstallerPath { get; }
    string? FailureMessage { get; }
    string? SkippedVersion { get; }
    DateTimeOffset? RemindLaterUntilUtc { get; }

    void ReportChecking();
    void ReportUpdateAvailable(UpdateInfo info);
    void ReportDownloading(int percent);
    void ReportReadyToInstall(UpdateInfo info, string installerPath);
    void ReportFailed(string message);
    void ReportUpdated(string version);
    void ReportIdle();

    void RemindLater(TimeSpan delay);
    void SkipVersion(string version);
}

public enum UpdateLifecycleStatus
{
    Idle,
    Checking,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    Updated,
    Failed
}
