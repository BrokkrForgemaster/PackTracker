using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Security;
using PackTracker.Presentation.Services;
using PackTracker.Presentation.Views;
using DomainRequestStatus = PackTracker.Domain.Enums.RequestStatus;
using DomainRequestKind = PackTracker.Domain.Enums.RequestKind;

namespace PackTracker.Presentation.ViewModels;

/// <summary>
/// ViewModel for the Assistance Hub — lists and manages general assistance requests.
/// </summary>
public partial class RequestsViewModel : ObservableObject
{
    #region Fields

    private readonly IApiClientProvider _apiClientProvider;
    private readonly SignalRChatService _signalR;
    private readonly IServiceProvider _services;
    private readonly ILogger<RequestsViewModel> _logger;
    private string? _currentRole;
    private string? _currentUsername;

    #endregion

    #region Constructor

    public RequestsViewModel(
        IApiClientProvider apiClientProvider,
        SignalRChatService signalR,
        IServiceProvider services,
        ILogger<RequestsViewModel> logger)
    {
        _apiClientProvider = apiClientProvider;
        _signalR = signalR;
        _services = services;
        _logger = logger;

        KindOptions = BuildKindOptions();
        StatusOptions = BuildStatusOptions();
        SelectedKind = null;
        SelectedStatus = null;

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        await LoadCurrentUserAsync();

        try
        {
            await _signalR.ConnectAsync();
            _signalR.AssistanceRequestCreated += OnAssistanceRequestEvent;
            _signalR.AssistanceRequestUpdated += OnAssistanceRequestEvent;
            _signalR.ConnectionStateChanged += connected =>
            {
                if (connected)
                    OnAssistanceRequestEvent(Guid.Empty);
            };
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "InitializeSignalR");
            StatusMessage = $"SignalR connection failed: {ex.Message}";
        }

        await RefreshAsync();
    }

    #endregion

    #region Collections

    public ObservableCollection<AssistanceRequestDto> Requests { get; } = new();

    /// <summary>
    /// Filter options for request kind. Null entry represents "All Kinds".
    /// </summary>
    public List<DomainRequestKind?> KindOptions { get; }

    /// <summary>
    /// Filter options for request status. Null entry represents "All Statuses".
    /// </summary>
    public List<string?> StatusOptions { get; }

    #endregion

    #region Observable Properties

    [ObservableProperty] private AssistanceRequestDto? selectedRequest;
    [ObservableProperty] private DomainRequestKind? selectedKind;
    [ObservableProperty] private string? selectedStatus;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Ready";

    #endregion

    #region Derived State

    /// <summary>
    /// True when the current user can claim the selected request.
    /// </summary>
    public bool CanClaim => SelectedRequest is not null
                         && !IsSelectedRequestClaimedByCurrentUser()
                         && SelectedRequest.Status == DomainRequestStatus.Open.ToString()
                         && SelectedRequest.ClaimCount < SelectedRequest.MaxClaims;

    public bool CanUnclaim => SelectedRequest is not null
                           && IsSelectedRequestClaimedByCurrentUser()
                           && SelectedRequest.Status != DomainRequestStatus.Completed.ToString()
                           && SelectedRequest.Status != DomainRequestStatus.Cancelled.ToString();

    /// <summary>
    /// True when the selected request can be marked complete.
    /// </summary>
    public bool CanComplete => SelectedRequest is not null
                            && SelectedRequest.Status != DomainRequestStatus.Completed.ToString()
                            && SelectedRequest.Status != DomainRequestStatus.Cancelled.ToString()
                            && string.Equals(SelectedRequest.CreatedByUsername, _currentUsername, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the selected request can be deleted (cancelled) by the current user.
    /// </summary>
    public bool CanDelete => SelectedRequest is not null
                          && SelectedRequest.Status != DomainRequestStatus.Completed.ToString()
                          && SelectedRequest.Status != DomainRequestStatus.Cancelled.ToString()
                          && (string.Equals(SelectedRequest.CreatedByUsername, _currentUsername, StringComparison.OrdinalIgnoreCase)
                              || SecurityConstants.IsElevatedRequestRole(_currentRole));

    /// <summary>
    /// True when the current user may edit the selected request (creator only, not terminal status).
    /// </summary>
    public bool CanEdit => SelectedRequest is not null
                        && SelectedRequest.Status != DomainRequestStatus.Completed.ToString()
                        && SelectedRequest.Status != DomainRequestStatus.Cancelled.ToString()
                        && string.Equals(SelectedRequest.CreatedByUsername, _currentUsername, StringComparison.OrdinalIgnoreCase);

    public bool CanPin => SelectedRequest is not null
                       && SecurityConstants.IsRallyMasterOrAbove(_currentRole);

    public string ClaimActionLabel => IsSelectedRequestClaimedByCurrentUser()
        ? "UNCLAIM REQUEST"
        : "CLAIM REQUEST";

    public string PinActionLabel => SelectedRequest?.IsPinned == true
        ? "UNPIN REQUEST"
        : "PIN REQUEST";

    #endregion

    #region Partial Hooks

    partial void OnSelectedRequestChanged(AssistanceRequestDto? value)
    {
        OnPropertyChanged(nameof(CanClaim));
        OnPropertyChanged(nameof(CanUnclaim));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanPin));
        OnPropertyChanged(nameof(ClaimActionLabel));
        OnPropertyChanged(nameof(PinActionLabel));
    }

    partial void OnSelectedKindChanged(DomainRequestKind? value)
    {
        _ = RefreshAsync();
    }

    partial void OnSelectedStatusChanged(string? value)
    {
        _ = RefreshAsync();
    }

    #endregion

    #region Commands

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await RefreshAsync(SelectedRequest?.Id);
    }

    private async Task RefreshAsync(Guid? requestIdToPreserve)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing...";

            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync(BuildRequestsQueryUrl());

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Server Error: {(int)response.StatusCode}";
                return;
            }

            var items = await response.Content.ReadFromJsonAsync<List<AssistanceRequestDto>>()
                        ?? new List<AssistanceRequestDto>();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Requests.Clear();
                foreach (var item in items)
                    Requests.Add(item);

                SelectedRequest = requestIdToPreserve.HasValue
                    ? Requests.FirstOrDefault(x => x.Id == requestIdToPreserve.Value)
                    : null;
            });

            StatusMessage = Requests.Count == 0
                ? "No requests found."
                : $"Loaded {Requests.Count} request(s).";
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(
                ex,
                "RefreshRequests",
                ("SelectedKind", SelectedKind?.ToString()),
                ("SelectedStatus", SelectedStatus),
                ("RequestUrl", BuildRequestsQueryUrl()));
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectRequest(AssistanceRequestDto? request)
    {
        SelectedRequest = request;
    }

    [RelayCommand]
    private async Task NewRequestAsync()
    {
        try
        {
            var vm = _services.GetRequiredService<NewRequestViewModel>();
            var dialog = new NewRequestDialog(vm);
            var result = dialog.ShowDialog();

            if (result == true)
                await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "OpenNewRequestDialog");
            StatusMessage = $"Failed to open dialog: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedRequest is null || !CanEdit) return;

        try
        {
            var vm = _services.GetRequiredService<NewRequestViewModel>();
            vm.LoadForEdit(SelectedRequest);
            var dialog = new NewRequestDialog(vm);
            var result = dialog.ShowDialog();

            if (result == true)
                await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "OpenEditRequestDialog");
            StatusMessage = $"Failed to open edit dialog: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClaimAsync()
    {
        if (SelectedRequest is null) return;

        var isClaimedByCurrentUser = IsSelectedRequestClaimedByCurrentUser();

        var url = isClaimedByCurrentUser
            ? $"api/v1/requests/{SelectedRequest.Id}/unclaim"
            : $"api/v1/requests/{SelectedRequest.Id}/claim";

        await PatchAsync(url, isClaimedByCurrentUser ? "Request unclaimed." : "Request claimed.");
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (SelectedRequest is null) return;

        await PatchAsync($"api/v1/requests/{SelectedRequest.Id}/complete", "Request completed.");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedRequest is null) return;

        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.DeleteAsync($"api/v1/requests/{SelectedRequest.Id}");

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Request cancelled.";
                await RefreshAsync();
            }
            else
            {
                StatusMessage = $"Cancel failed ({(int)response.StatusCode})";
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(
                ex,
                "DeleteRequest",
                ("RequestId", SelectedRequest.Id),
                ("SelectedStatus", SelectedRequest.Status));
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task TogglePinAsync()
    {
        if (SelectedRequest is null || !CanPin)
            return;

        var url = SelectedRequest.IsPinned
            ? $"api/v1/requests/{SelectedRequest.Id}/unpin"
            : $"api/v1/requests/{SelectedRequest.Id}/pin";

        await PatchAsync(url, SelectedRequest.IsPinned ? "Request unpinned." : "Request pinned.");
    }

    #endregion

    #region Helpers

    private async Task PatchAsync(string url, string successMessage)
    {
        try
        {
            IsLoading = true;
            var selectedRequestId = SelectedRequest?.Id;
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.PatchAsync(url, null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = successMessage;
                await RefreshAsync(selectedRequestId);
            }
            else
            {
                StatusMessage = $"Update failed ({(int)response.StatusCode})";
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(
                ex,
                "PatchRequest",
                ("RequestUrl", url),
                ("SelectedRequestId", SelectedRequest?.Id),
                ("SuccessMessage", successMessage));
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnAssistanceRequestEvent(Guid id)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { _ = RefreshAsync(); });
    }

    private async Task LoadCurrentUserAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync("api/v1/profiles/me");
            if (!response.IsSuccessStatusCode)
                return;

            var profile = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
            _currentRole = profile?.DiscordRank;
            _currentUsername = profile?.Username;
            OnPropertyChanged(nameof(CanPin));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(CanClaim));
            OnPropertyChanged(nameof(CanUnclaim));
            OnPropertyChanged(nameof(ClaimActionLabel));
            OnPropertyChanged(nameof(PinActionLabel));
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "LoadCurrentRequestUser");
        }
    }

    private static List<DomainRequestKind?> BuildKindOptions()
    {
        var list = new List<DomainRequestKind?> { null };
        foreach (var kind in Enum.GetValues<DomainRequestKind>())
            list.Add(kind);
        return list;
    }

    private static List<string?> BuildStatusOptions() =>
        new()
        {
            null,
            DomainRequestStatus.Open.ToString(),
            DomainRequestStatus.Accepted.ToString(),
            DomainRequestStatus.InProgress.ToString(),
            DomainRequestStatus.Completed.ToString(),
            DomainRequestStatus.Cancelled.ToString()
        };

    private string BuildRequestsQueryUrl()
    {
        var queryParts = new List<string>();

        if (SelectedKind.HasValue)
            queryParts.Add($"kind={Uri.EscapeDataString(SelectedKind.Value.ToString())}");

        if (!string.IsNullOrWhiteSpace(SelectedStatus))
            queryParts.Add($"status={Uri.EscapeDataString(SelectedStatus)}");

        if (queryParts.Count == 0)
            return "api/v1/requests";

        var builder = new StringBuilder("api/v1/requests?");
        builder.Append(string.Join("&", queryParts));
        return builder.ToString();
    }

    private bool IsSelectedRequestClaimedByCurrentUser()
    {
        if (SelectedRequest is null)
            return false;

        if (SelectedRequest.IsClaimedByCurrentUser)
            return true;

        return !string.IsNullOrWhiteSpace(_currentUsername)
            && string.Equals(SelectedRequest.AssignedToUsername, _currentUsername, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CurrentUserDto(string Username, string? DiscordRank);

    #endregion
}
