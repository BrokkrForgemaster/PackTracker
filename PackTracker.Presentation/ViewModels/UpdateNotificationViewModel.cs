using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Presentation.Commands;

namespace PackTracker.Presentation.ViewModels;

/// <summary>
/// View model that mirrors <see cref="IUpdateNotificationState"/> onto the UI thread
/// and exposes commands for the banner / dialog actions.
/// </summary>
public sealed class UpdateNotificationViewModel : ViewModelBase, IDisposable
{
    private readonly IUpdateNotificationState _state;
    private readonly IUpdateService _updateService;
    private readonly UpdateOptions _options;
    private readonly ILogger<UpdateNotificationViewModel> _logger;
    private readonly Dispatcher _dispatcher;

    private bool _isDialogOpen;
    private bool _isLaunchingInstaller;

    public UpdateNotificationViewModel(
        IUpdateNotificationState state,
        IUpdateService updateService,
        IOptions<UpdateOptions> options,
        ILogger<UpdateNotificationViewModel> logger)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _state.PropertyChanged += OnStateChanged;

        OpenDialogCommand    = new RelayCommand(() => IsDialogOpen = true, () => HasActiveUpdate);
        CloseDialogCommand   = new RelayCommand(() => IsDialogOpen = false);
        RestartNowCommand    = new RelayCommand(RestartNow,    () => CanRestartNow);
        RemindLaterCommand   = new RelayCommand(RemindLater,   () => HasActiveUpdate);
        SkipVersionCommand   = new RelayCommand(SkipVersion,   () => HasActiveUpdate);
        DismissUpdateCommand = new RelayCommand(DismissUpdate);
    }

    public ICommand OpenDialogCommand { get; }
    public ICommand CloseDialogCommand { get; }
    public ICommand RestartNowCommand { get; }
    public ICommand RemindLaterCommand { get; }
    public ICommand SkipVersionCommand { get; }
    public ICommand DismissUpdateCommand { get; }

    public UpdateLifecycleStatus Status => _state.Status;

    public bool ShowBanner =>
        Status is UpdateLifecycleStatus.UpdateAvailable
                or UpdateLifecycleStatus.Downloading
                or UpdateLifecycleStatus.ReadyToInstall
                or UpdateLifecycleStatus.Updated
                or UpdateLifecycleStatus.Failed;

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set => SetProperty(ref _isDialogOpen, value);
    }

    public bool HasActiveUpdate => _state.AvailableUpdate is not null;

    public bool IsDownloading => Status == UpdateLifecycleStatus.Downloading;
    public bool IsReadyToInstall => Status == UpdateLifecycleStatus.ReadyToInstall;
    public bool IsUpdated => Status == UpdateLifecycleStatus.Updated;
    public bool IsFailed => Status == UpdateLifecycleStatus.Failed;
    public bool CanRestartNow => IsReadyToInstall && !_isLaunchingInstaller;

    public int DownloadProgress => _state.DownloadProgress;

    public string VersionLabel
    {
        get
        {
            var info = _state.AvailableUpdate;
            if (info is null) return string.Empty;

            return info.PublishedAt is { } published
                ? $"v{info.Version}  ·  {published:MMM d, yyyy}"
                : $"v{info.Version}";
        }
    }

    public string ReleaseNotes
    {
        get
        {
            var notes = _state.AvailableUpdate?.ReleaseNotes;
            return string.IsNullOrWhiteSpace(notes)
                ? "No release notes provided."
                : notes.Trim();
        }
    }

    public string BannerHeadline => Status switch
    {
        UpdateLifecycleStatus.UpdateAvailable => $"Update available · v{_state.AvailableUpdate?.Version}",
        UpdateLifecycleStatus.Downloading     => $"Downloading update · {_state.DownloadProgress}%",
        UpdateLifecycleStatus.ReadyToInstall  => $"Ready to install · v{_state.AvailableUpdate?.Version}",
        UpdateLifecycleStatus.Updated         => "Update successful!",
        UpdateLifecycleStatus.Failed          => "Update check failed",
        _ => string.Empty
    };

    public string BannerSubtext => Status switch
    {
        UpdateLifecycleStatus.UpdateAvailable => "Click for release notes",
        UpdateLifecycleStatus.Downloading     => "You can keep working — we'll notify you when it's ready",
        UpdateLifecycleStatus.ReadyToInstall  => "Restart when you're ready",
        UpdateLifecycleStatus.Updated         => $"You are now running the latest version (v{_state.AvailableUpdate?.Version})",
        UpdateLifecycleStatus.Failed          => _state.FailureMessage ?? "See logs for details",
        _ => string.Empty
    };

    public string PrimaryActionLabel => Status switch
    {
        UpdateLifecycleStatus.ReadyToInstall => "RESTART NOW",
        UpdateLifecycleStatus.Updated        => "DISMISS",
        _                                    => "VIEW DETAILS"
    };

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Marshal everything to the UI thread; bindings need it.
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => OnStateChanged(sender, e));
            return;
        }

        // Coarse-grained: just notify everything that depends on state.
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ShowBanner));
        OnPropertyChanged(nameof(HasActiveUpdate));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsReadyToInstall));
        OnPropertyChanged(nameof(IsUpdated));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(CanRestartNow));
        OnPropertyChanged(nameof(DownloadProgress));
        OnPropertyChanged(nameof(VersionLabel));
        OnPropertyChanged(nameof(ReleaseNotes));
        OnPropertyChanged(nameof(BannerHeadline));
        OnPropertyChanged(nameof(BannerSubtext));
        OnPropertyChanged(nameof(PrimaryActionLabel));

        (OpenDialogCommand    as RelayCommand)?.RaiseCanExecuteChanged();
        (RestartNowCommand    as RelayCommand)?.RaiseCanExecuteChanged();
        (RemindLaterCommand   as RelayCommand)?.RaiseCanExecuteChanged();
        (SkipVersionCommand   as RelayCommand)?.RaiseCanExecuteChanged();
        (DismissUpdateCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Auto-open the dialog the first time an update is announced so the user sees it.
        if (Status == UpdateLifecycleStatus.ReadyToInstall && !IsDialogOpen)
        {
            // Don't steal focus on auto-open; just surface it gently via the banner.
        }
    }

    private async void RestartNow()
    {
        var path = _state.StagedInstallerPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _isLaunchingInstaller = true;
        (RestartNowCommand as RelayCommand)?.RaiseCanExecuteChanged();

        try
        {
            await _updateService.InstallAndRestartAsync(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch installer.");
            _state.ReportFailed(ex.Message);
            _isLaunchingInstaller = false;
            (RestartNowCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void RemindLater()
    {
        var hours = _options.RemindLaterHours > 0 ? _options.RemindLaterHours : 24.0;
        _state.RemindLater(TimeSpan.FromHours(hours));
        IsDialogOpen = false;
    }

    private void SkipVersion()
    {
        var version = _state.AvailableUpdate?.Version;
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        _state.SkipVersion(version);
        IsDialogOpen = false;
    }

    private void DismissUpdate()
    {
        _state.ReportIdle();
        IsDialogOpen = false;
    }

    public void Dispose()
    {
        _state.PropertyChanged -= OnStateChanged;
    }
}
