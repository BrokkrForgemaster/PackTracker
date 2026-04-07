using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class ProcurementRequestsViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClientProvider;

    public ObservableCollection<MaterialProcurementRequestListItemDto> Requests { get; } = new();

    [ObservableProperty] private MaterialProcurementRequestListItemDto? selectedRequest;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Loading material procurement requests...";
    [ObservableProperty] private string newCommentText = string.Empty;

    public ObservableCollection<RequestCommentDto> Comments { get; } = new();

    public bool CanClaim => SelectedRequest is not null && SelectedRequest.Status == "Open";
    public bool CanRefuse => SelectedRequest is not null && (SelectedRequest.Status == "Open" || SelectedRequest.Status == "InProgress");
    public bool CanMarkInProgress => SelectedRequest is not null && SelectedRequest.Status == "Open";
    public bool CanMarkCompleted => SelectedRequest is not null
        && (SelectedRequest.Status == "Open" || SelectedRequest.Status == "InProgress");
    public bool CanCancel => SelectedRequest is not null
        && SelectedRequest.Status != "Completed" && SelectedRequest.Status != "Cancelled";

    public ProcurementRequestsViewModel(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
        StatusMessage = "Loading material procurement requests...";
        _ = RefreshAsync();
    }

    partial void OnSelectedRequestChanged(MaterialProcurementRequestListItemDto? value)
    {
        OnPropertyChanged(nameof(CanClaim));
        OnPropertyChanged(nameof(CanRefuse));
        OnPropertyChanged(nameof(CanMarkInProgress));
        OnPropertyChanged(nameof(CanMarkCompleted));
        OnPropertyChanged(nameof(CanCancel));

        if (value is not null)
            _ = LoadCommentsAsync(value.Id);
        else
            Comments.Clear();
    }

    private async Task LoadCommentsAsync(Guid requestId)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            var comments = await client.GetFromJsonAsync<List<RequestCommentDto>>($"api/v1/crafting/procurement-requests/{requestId}/comments");
            
            Comments.Clear();
            if (comments != null)
            {
                foreach (var c in comments)
                    Comments.Add(c);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Comment load failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing procurement requests...";

            using var client = _apiClientProvider.CreateClient();
            var items = await client.GetFromJsonAsync<List<MaterialProcurementRequestListItemDto>>("api/v1/crafting/procurement-requests")
                        ?? new List<MaterialProcurementRequestListItemDto>();

            Requests.Clear();
            foreach (var item in items)
                Requests.Add(item);

            StatusMessage = Requests.Count == 0
                ? "No procurement requests yet."
                : $"Loaded {Requests.Count} procurement requests.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load procurement requests: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddCommentAsync()
    {
        if (SelectedRequest is null || string.IsNullOrWhiteSpace(NewCommentText)) return;

        try
        {
            using var client = _apiClientProvider.CreateClient();
            var response = await client.PostAsJsonAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/comments", 
                new AddRequestCommentDto { Content = NewCommentText });

            if (response.IsSuccessStatusCode)
            {
                NewCommentText = string.Empty;
                await LoadCommentsAsync(SelectedRequest.Id);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Comment failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClaimAsync()
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/claim", null,
            "Request claimed — status set to In Progress.");
    }

    [RelayCommand]
    private async Task RefuseAsync()
    {
        if (SelectedRequest is null) return;
        
        var reason = "Cannot fulfill procurement at this time.";
        
        await PatchAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/refuse", 
            new RefuseRequestDto { Reason = reason },
            $"Request refused.");
    }

    [RelayCommand]
    private async Task MarkInProgressAsync()
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/status",
            new UpdateRequestStatusDto { Status = "InProgress" },
            "Request marked as In Progress.");
    }

    [RelayCommand]
    private async Task MarkCompletedAsync()
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/status",
            new UpdateRequestStatusDto { Status = "Completed" },
            "Request marked as Completed.");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/status",
            new UpdateRequestStatusDto { Status = "Cancelled" },
            "Request cancelled.");
    }

    // ─── HELPERS ────────────────────────────────────────────────────────────────

    private async Task PatchAsync(string url, object? body, string successMessage)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Updating request...";

            using var client = _apiClientProvider.CreateClient();
            HttpResponseMessage response;

            if (body is null)
                response = await client.PatchAsync(url, null);
            else
                response = await client.PatchAsJsonAsync(url, body);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                StatusMessage = $"Error {(int)response.StatusCode}: {detail}";
                return;
            }

            StatusMessage = successMessage;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Action failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
