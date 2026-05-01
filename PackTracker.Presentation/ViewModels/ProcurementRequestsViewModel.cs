using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class ProcurementRequestsViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly SignalRChatService _signalR;
    private readonly ILogger<ProcurementRequestsViewModel> _logger;
    private CancellationTokenSource? _loadCommentsCts;
    private string _currentUsername = string.Empty;
    private string? _currentRole;

    public ObservableCollection<MaterialProcurementRequestListItemDto> Requests { get; } = new();
    public ObservableCollection<RequestCommentDto> Comments { get; } = new();

    [ObservableProperty] private MaterialProcurementRequestListItemDto? selectedRequest;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Ready";
    [ObservableProperty] private string newCommentText = string.Empty;

    #region Command Execution Logic

    public bool CanClaim => SelectedRequest?.Status == RequestStatus.Open;

    public bool CanMarkInProgress => SelectedRequest is not null &&
                                     (SelectedRequest.Status == RequestStatus.Open ||
                                      SelectedRequest.Status == RequestStatus.Accepted) &&
                                     (string.Equals(SelectedRequest.RequesterUsername, _currentUsername, StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(SelectedRequest.AssignedToUsername, _currentUsername, StringComparison.OrdinalIgnoreCase));

    public bool CanMarkCompleted => SelectedRequest is not null &&
                                   (SelectedRequest.Status == RequestStatus.Open ||
                                    SelectedRequest.Status == RequestStatus.Accepted ||
                                    SelectedRequest.Status == RequestStatus.InProgress) &&
                                   (string.Equals(SelectedRequest.RequesterUsername, _currentUsername, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(SelectedRequest.AssignedToUsername, _currentUsername, StringComparison.OrdinalIgnoreCase));

    // Only the original requester may cancel
    public bool CanCancel => SelectedRequest is not null &&
                             SelectedRequest.Status != RequestStatus.Completed &&
                             SelectedRequest.Status != RequestStatus.Cancelled &&
                             (SelectedRequest.RequesterUsername == _currentUsername || IsPrivilegedRole(_currentRole));

    #endregion

    public ProcurementRequestsViewModel(
        IApiClientProvider apiClientProvider,
        SignalRChatService signalR,
        ILogger<ProcurementRequestsViewModel> logger)
    {
        _apiClientProvider = apiClientProvider;
        _signalR = signalR;
        _logger = logger;
        _ = LoadCurrentUserAsync();
        _ = InitSignalRAsync();
    }

    private async Task InitSignalRAsync()
    {
        try
        {
            await _signalR.ConnectAsync();
            _signalR.ProcurementRequestCreated += OnProcurementEvent;
            _signalR.ProcurementRequestUpdated += OnProcurementEvent;
            _signalR.ConnectionStateChanged += connected =>
            {
                if (connected)
                    OnProcurementEvent(Guid.Empty);
            };
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "ConnectProcurementSignalR");
        }
    }

    private async Task LoadCurrentUserAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync("api/v1/profiles/me");
            if (response.IsSuccessStatusCode)
            {
                var profile = await response.Content.ReadFromJsonAsync<ProfileSummary>();
                _currentUsername = profile?.Username ?? string.Empty;
                _currentRole = profile?.DiscordRank;
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "LoadCurrentUser");
        }
        finally
        {
            await RefreshAsync();
        }
    }

    private record ProfileSummary(string Username, string? DiscordRank);

    partial void OnSelectedRequestChanged(MaterialProcurementRequestListItemDto? value)
    {
        ClaimCommand.NotifyCanExecuteChanged();
        MarkInProgressCommand.NotifyCanExecuteChanged();
        MarkCompletedCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(CanClaim));
        OnPropertyChanged(nameof(CanMarkInProgress));
        OnPropertyChanged(nameof(CanMarkCompleted));
        OnPropertyChanged(nameof(CanCancel));

        // Cancel pending comment loads to prevent race conditions
        _loadCommentsCts?.Cancel();
        _loadCommentsCts = new CancellationTokenSource();

        if (value is not null)
        {
            _ = LoadCommentsAsync(value.Id, _loadCommentsCts.Token);
        }
        else
        {
            Comments.Clear();
        }
    }

    private async Task LoadCommentsAsync(Guid requestId, CancellationToken ct)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync($"api/v1/crafting/procurement-requests/{requestId}/comments", ct);
            if (!response.IsSuccessStatusCode) return;
            var comments = await response.Content.ReadFromJsonAsync<List<RequestCommentDto>>(cancellationToken: ct);

            if (ct.IsCancellationRequested) return;

            Comments.Clear();
            if (comments != null)
            {
                foreach (var c in comments)
                    Comments.Add(c);
            }
        }
        catch (OperationCanceledException) { /* Normal during rapid selection */ }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "LoadProcurementComments", ("RequestId", requestId));
            StatusMessage = $"Comment load failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing procurement queue...";

            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync("api/v1/crafting/procurement-requests");

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Server Error: {(int)response.StatusCode}";
                return;
            }

            var items = await response.Content.ReadFromJsonAsync<List<MaterialProcurementRequestListItemDto>>()
                        ?? new List<MaterialProcurementRequestListItemDto>();

            Requests.Clear();
            foreach (var item in items)
                Requests.Add(item);

            StatusMessage = Requests.Count == 0 ? "No active procurements." : $"Found {Requests.Count} items.";
            
            // Ensure UI updates if the list refresh changed the selected item's status
            OnSelectedRequestChanged(SelectedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "RefreshProcurementRequests");
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task RefreshDataAsync() => RefreshAsync();

    [RelayCommand]
    private async Task AddCommentAsync()
    {
        if (SelectedRequest is null || string.IsNullOrWhiteSpace(NewCommentText)) return;

        try
        {
            using var client = _apiClientProvider.CreateClient();
            var response = await client.PostAsJsonAsync(
                $"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/comments",
                new AddRequestCommentDto { Content = NewCommentText });

            if (response.IsSuccessStatusCode)
            {
                NewCommentText = string.Empty;
                await LoadCommentsAsync(SelectedRequest.Id, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "AddProcurementComment", ("RequestId", SelectedRequest.Id));
            StatusMessage = $"Comment failed: {ex.Message}";
        }
    }

    #region Action Commands

    [RelayCommand(CanExecute = nameof(CanClaim))]
    private async Task ClaimAsync()
    {
        await PatchAsync($"api/v1/crafting/procurement-requests/{SelectedRequest!.Id}/claim", null, "Claimed.");
    }

   
    [RelayCommand(CanExecute = nameof(CanMarkInProgress))]
    private async Task MarkInProgressAsync() => await PatchStatusAsync(RequestStatus.InProgress, "In Progress.");

    [RelayCommand(CanExecute = nameof(CanMarkCompleted))]
    private async Task MarkCompletedAsync() => await PatchStatusAsync(RequestStatus.Completed, "Completed.");

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync()
    {
        if (SelectedRequest is null) return;

        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.DeleteAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}");

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Request removed.";
                await RefreshAsync();
            }
            else
            {
                StatusMessage = $"Error: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "CancelProcurementRequest", ("RequestId", SelectedRequest.Id));
            StatusMessage = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Helpers

    private async Task PatchStatusAsync(string status, string message)
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/status",
            new UpdateRequestStatusDto { Status = status }, message);
    }

    private async Task PatchAsync(string url, object? body, string successMessage)
    {
        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            
            var response = body is null 
                ? await client.PatchAsync(url, null) 
                : await client.PatchAsJsonAsync(url, body);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = successMessage;
                await RefreshAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                StatusMessage = $"Error: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(
                ex,
                "PatchProcurementRequest",
                ("RequestUrl", url),
                ("RequestId", SelectedRequest?.Id),
                ("HasBody", body is not null));
            StatusMessage = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnProcurementEvent(Guid id)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { _ = RefreshAsync(); });
    }

    private static bool IsPrivilegedRole(string? role) =>
        SecurityConstants.IsElevatedRequestRole(role);

    #endregion
}
