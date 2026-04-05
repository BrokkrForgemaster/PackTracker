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

    public bool CanClaim => SelectedRequest is not null && SelectedRequest.Status == "Open";
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
        OnPropertyChanged(nameof(CanMarkCompleted));
        OnPropertyChanged(nameof(CanCancel));
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
    private async Task ClaimAsync()
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/procurement-requests/{SelectedRequest.Id}/claim", null,
            "Request claimed — status set to In Progress.");
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
