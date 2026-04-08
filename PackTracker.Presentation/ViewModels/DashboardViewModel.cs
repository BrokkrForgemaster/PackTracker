using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClient;
    private readonly IUpdateService _updateService;

    public GuideDashboardViewModel Guide { get; }
    public ObservableCollection<RequestDto> ActiveRequests { get; } = new();

    [ObservableProperty]
    private UpdateState updateState = UpdateState.Checking;

    [ObservableProperty]
    private UpdateInfo? availableUpdate;

    [ObservableProperty]
    private int updateProgress;

    [ObservableProperty]
    private string updateStatusMessage = "Checking for updates...";

    public string OperationButtonText => UpdateState switch
    {
        UpdateState.Checking => "Checking for Updates...",
        UpdateState.UpdateAvailable => "Download Update",
        UpdateState.Downloading => $"Downloading... {UpdateProgress}%",
        UpdateState.Installing => "Installing Update...",
        UpdateState.Failed => "Retry Update",
        _ => "Operational Procedure"
    };

    public bool IsBusy =>
        UpdateState == UpdateState.Checking ||
        UpdateState == UpdateState.Downloading ||
        UpdateState == UpdateState.Installing;

    public DashboardViewModel(
        GuideDashboardViewModel guide,
        IApiClientProvider apiClient,
        IUpdateService updateService)
    {
        Guide = guide ?? throw new ArgumentNullException(nameof(guide));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));

        _ = InitializeAsync();
    }

    partial void OnUpdateStateChanged(UpdateState value)
    {
        OnPropertyChanged(nameof(OperationButtonText));
        OnPropertyChanged(nameof(IsBusy));
        RunOperationCommand.NotifyCanExecuteChanged();
        RefreshDashboardCommand.NotifyCanExecuteChanged();
    }

    partial void OnUpdateProgressChanged(int value)
    {
        OnPropertyChanged(nameof(OperationButtonText));
    }

    private async Task InitializeAsync()
    {
        await Task.WhenAll(
            Guide.RefreshAsync(),
            LoadActiveRequestsAsync(),
            RefreshUpdateStateAsync());
    }

    [RelayCommand(CanExecute = nameof(CanRefreshDashboard))]
    private async Task RefreshDashboardAsync()
    {
        await Task.WhenAll(
            Guide.RefreshAsync(),
            LoadActiveRequestsAsync(),
            RefreshUpdateStateAsync());
    }

    private bool CanRefreshDashboard() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private async Task RunOperationAsync()
    {
        try
        {
            if (UpdateState == UpdateState.UpdateAvailable || UpdateState == UpdateState.Failed)
            {
                await DownloadAndInstallUpdateAsync();
                return;
            }

            OpenOperationalProcedure();
        }
        catch (Exception ex)
        {
            UpdateState = UpdateState.Failed;
            UpdateStatusMessage = $"Update failed: {ex.Message}";
        }
    }

    private bool CanRunOperation()
    {
        return UpdateState != UpdateState.Downloading &&
               UpdateState != UpdateState.Installing;
    }

    public async Task RefreshUpdateStateAsync()
    {
        try
        {
            UpdateState = UpdateState.Checking;
            UpdateProgress = 0;
            UpdateStatusMessage = $"Current version: {_updateService.GetCurrentVersion()}";

            AvailableUpdate = await _updateService.CheckForUpdateAsync();

            if (AvailableUpdate != null)
            {
                UpdateState = UpdateState.UpdateAvailable;
                UpdateStatusMessage = $"Version {AvailableUpdate.Version} is available.";
            }
            else
            {
                UpdateState = UpdateState.Normal;
                UpdateStatusMessage = "Application is up to date.";
            }
        }
        catch (Exception ex)
        {
            AvailableUpdate = null;
            UpdateState = UpdateState.Failed;
            UpdateStatusMessage = $"Could not check for updates: {ex.Message}";
        }
    }

    private async Task DownloadAndInstallUpdateAsync()
    {
        if (AvailableUpdate == null)
        {
            await RefreshUpdateStateAsync();
            return;
        }

        UpdateProgress = 0;
        UpdateState = UpdateState.Downloading;
        UpdateStatusMessage = $"Downloading version {AvailableUpdate.Version}...";

        var progress = new Progress<int>(value =>
        {
            UpdateProgress = value;
            UpdateStatusMessage = $"Downloading version {AvailableUpdate.Version}... {value}%";
        });

        var installerPath = await _updateService.DownloadUpdateAsync(AvailableUpdate, progress);

        UpdateState = UpdateState.Installing;
        UpdateStatusMessage = "Launching installer and restarting application...";

        await _updateService.InstallAndRestartAsync(installerPath);
    }

    private void OpenOperationalProcedure()
    {
        UpdateStatusMessage = "Opening operational procedure...";
        // TODO: Replace this with your real navigation or launch logic.
        // Example:
        // _navigationService.NavigateToOperations();
        // or
        // Process.Start(new ProcessStartInfo(...));
    }

    private async Task LoadActiveRequestsAsync()
    {
        try
        {
            using var client = _apiClient.CreateClient();
            var response = await client.GetFromJsonAsync<ApiResponse<List<RequestDto>>>(
                "api/v1/requests?status=0&top=5");

            ActiveRequests.Clear();

            if (response?.Data != null)
            {
                foreach (var request in response.Data.OrderByDescending(r => r.Priority))
                {
                    ActiveRequests.Add(request);
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Failed to load active requests: {ex.Message}";
        }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public int? Count { get; set; }
        public T? Data { get; set; }
    }
}

public enum UpdateState
{
    Normal,
    Checking,
    UpdateAvailable,
    Downloading,
    Installing,
    Failed
}