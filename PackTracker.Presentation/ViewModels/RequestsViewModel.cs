using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.DTOs.Request;
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

    #endregion

    #region Constructor

    public RequestsViewModel(
        IApiClientProvider apiClientProvider,
        SignalRChatService signalR,
        IServiceProvider services)
    {
        _apiClientProvider = apiClientProvider;
        _signalR = signalR;
        _services = services;

        KindOptions = BuildKindOptions();
        StatusOptions = BuildStatusOptions();
        SelectedKind = null;
        SelectedStatus = null;

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
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
    /// True when the current user can claim the selected request (it is Open with no assignee).
    /// </summary>
    public bool CanClaim => SelectedRequest is not null
                         && SelectedRequest.Status == DomainRequestStatus.Open.ToString()
                         && string.IsNullOrEmpty(SelectedRequest.AssignedToUsername);

    /// <summary>
    /// True when the selected request can be marked complete.
    /// </summary>
    public bool CanComplete => SelectedRequest is not null
                            && SelectedRequest.Status != DomainRequestStatus.Completed.ToString()
                            && SelectedRequest.Status != DomainRequestStatus.Cancelled.ToString();

    /// <summary>
    /// True when the selected request can be deleted (cancelled) by the current user.
    /// Always shown; the server enforces creator-only restriction.
    /// </summary>
    public bool CanDelete => SelectedRequest is not null
                          && SelectedRequest.Status != DomainRequestStatus.Completed.ToString()
                          && SelectedRequest.Status != DomainRequestStatus.Cancelled.ToString();

    #endregion

    #region Partial Hooks

    partial void OnSelectedRequestChanged(AssistanceRequestDto? value)
    {
        OnPropertyChanged(nameof(CanClaim));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(CanDelete));
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
        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing...";

            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync("api/v1/requests");

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Server Error: {(int)response.StatusCode}";
                return;
            }

            var items = await response.Content.ReadFromJsonAsync<List<AssistanceRequestDto>>()
                        ?? new List<AssistanceRequestDto>();

            // Apply client-side filters
            if (SelectedKind.HasValue)
                items = items.Where(x => x.Kind == SelectedKind.Value).ToList();

            if (!string.IsNullOrEmpty(SelectedStatus))
                items = items.Where(x => x.Status == SelectedStatus).ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Requests.Clear();
                foreach (var item in items)
                    Requests.Add(item);
            });

            StatusMessage = Requests.Count == 0
                ? "No requests found."
                : $"Loaded {Requests.Count} request(s).";
        }
        catch (Exception ex)
        {
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
            StatusMessage = $"Failed to open dialog: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClaimAsync()
    {
        if (SelectedRequest is null) return;

        await PatchAsync($"api/v1/requests/{SelectedRequest.Id}/claim", "Request claimed.");
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
                SelectedRequest = null;
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
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Helpers

    private async Task PatchAsync(string url, string successMessage)
    {
        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.PatchAsync(url, null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = successMessage;
                SelectedRequest = null;
                await RefreshAsync();
            }
            else
            {
                StatusMessage = $"Update failed ({(int)response.StatusCode})";
            }
        }
        catch (Exception ex)
        {
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

    #endregion
}
