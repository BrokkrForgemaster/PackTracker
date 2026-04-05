using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class CraftingRequestsViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClientProvider;

    public ObservableCollection<CraftingRequestListItemDto> Requests { get; } = new();

    [ObservableProperty] private CraftingRequestListItemDto? selectedRequest;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Loading crafting requests...";

    public bool CanAssign => SelectedRequest is not null && SelectedRequest.Status == "Open";
    public bool CanMarkInProgress => SelectedRequest is not null && SelectedRequest.Status == "Open";
    public bool CanMarkCompleted => SelectedRequest is not null
        && (SelectedRequest.Status == "Open" || SelectedRequest.Status == "InProgress");
    public bool CanCancel => SelectedRequest is not null
        && SelectedRequest.Status != "Completed" && SelectedRequest.Status != "Cancelled";

    public CraftingRequestsViewModel(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
        StatusMessage = "Loading crafting requests...";
        _ = RefreshAsync();
    }

    partial void OnSelectedRequestChanged(CraftingRequestListItemDto? value)
    {
        OnPropertyChanged(nameof(CanAssign));
        OnPropertyChanged(nameof(CanMarkInProgress));
        OnPropertyChanged(nameof(CanMarkCompleted));
        OnPropertyChanged(nameof(CanCancel));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing crafting requests...";

            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync("api/v1/crafting/requests");

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Server Error: {(int)response.StatusCode} {response.ReasonPhrase}";
                return;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != "application/json" && !(contentType?.Contains("json") ?? false))
            {
                StatusMessage = "API returned HTML instead of JSON. Check your ApiBaseUrl.";
                return;
            }

            var items = await response.Content.ReadFromJsonAsync<List<CraftingRequestListItemDto>>()
                        ?? new List<CraftingRequestListItemDto>();

            Requests.Clear();
            foreach (var item in items)
                Requests.Add(item);

            StatusMessage = Requests.Count == 0
                ? "No crafting requests yet."
                : $"Loaded {Requests.Count} crafting requests.";
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
    private async Task AssignToSelfAsync()
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/assign", null,
            $"Assigned to self — status set to In Progress.");
    }

    [RelayCommand]
    private async Task MarkInProgressAsync()
    {
        if (SelectedRequest is null) return;
        await PatchWithStatusAsync(SelectedRequest.Id, "InProgress",
            "api/v1/crafting/requests", "Marked as In Progress.");
    }

    [RelayCommand]
    private async Task MarkCompletedAsync()
    {
        if (SelectedRequest is null) return;
        await PatchWithStatusAsync(SelectedRequest.Id, "Completed",
            "api/v1/crafting/requests", "Marked as Completed.");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (SelectedRequest is null) return;
        await PatchWithStatusAsync(SelectedRequest.Id, "Cancelled",
            "api/v1/crafting/requests", "Request cancelled.");
    }

    // ─── HELPERS ────────────────────────────────────────────────────────────────

    private async Task PatchWithStatusAsync(Guid id, string newStatus, string baseRoute, string successMessage)
    {
        await PatchAsync($"{baseRoute}/{id}/status",
            new UpdateRequestStatusDto { Status = newStatus },
            successMessage);
    }

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
