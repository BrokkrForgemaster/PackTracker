using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Enums;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class CraftingRequestsViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClientProvider;

    public ObservableCollection<CraftingRequestListItemDto> Requests { get; } = new();

    [ObservableProperty] private CraftingRequestListItemDto? selectedRequest;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Loading crafting requests...";
    [ObservableProperty] private string newCommentText = string.Empty;

    public ObservableCollection<RequestCommentDto> Comments { get; } = new();
    public ObservableCollection<BlueprintRecipeMaterialDto> RequiredMaterials { get; } = new();

    public bool CanAssign => SelectedRequest is not null && SelectedRequest.Status == "Open";
    public bool CanRefuse => SelectedRequest is not null && (SelectedRequest.Status == "Open" || SelectedRequest.Status == "Accepted");
    public bool CanMarkInProgress => SelectedRequest is not null && SelectedRequest.Status == "Accepted";
    public bool CanMarkCompleted => SelectedRequest is not null
        && (SelectedRequest.Status == "Accepted" || SelectedRequest.Status == "InProgress");
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
        OnPropertyChanged(nameof(CanRefuse));
        OnPropertyChanged(nameof(CanMarkInProgress));
        OnPropertyChanged(nameof(CanMarkCompleted));
        OnPropertyChanged(nameof(CanCancel));

        RequiredMaterials.Clear();
        if (value?.Materials != null)
        {
            foreach (var m in value.Materials)
                RequiredMaterials.Add(m);
        }

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
            var comments = await client.GetFromJsonAsync<List<RequestCommentDto>>($"api/v1/crafting/requests/{requestId}/comments");
            
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
    private async Task AddCommentAsync()
    {
        if (SelectedRequest is null || string.IsNullOrWhiteSpace(NewCommentText)) return;

        try
        {
            using var client = _apiClientProvider.CreateClient();
            var response = await client.PostAsJsonAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/comments", 
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
    private async Task AssignToSelfAsync()
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/assign", null,
            $"Assigned to self — status set to Accepted.");
    }

    [RelayCommand]
    private async Task RefuseAsync()
    {
        if (SelectedRequest is null) return;
        
        // Simple prompt for reason - note: in a real app this would be a better UI
        var reason = "Insufficient materials or quality level not achievable.";
        
        await PatchAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/refuse", 
            new RefuseRequestDto { Reason = reason },
            $"Request refused.");
    }

    [RelayCommand]
    private async Task RequestMaterialAsync(BlueprintRecipeMaterialDto material)
    {
        if (SelectedRequest is null || material is null) return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Spawning procurement for {material.MaterialName}...";

            using var client = _apiClientProvider.CreateClient();
            var dto = new CreateMaterialProcurementRequestDto
            {
                MaterialId = material.MaterialId,
                LinkedCraftingRequestId = SelectedRequest.Id,
                QuantityRequested = (decimal)material.QuantityRequired,
                Priority = RequestPriority.Normal,
                Notes = $"Auto-spawned for crafting request: {SelectedRequest.CraftedItemName}"
            };

            var response = await client.PostAsJsonAsync("api/v1/crafting/procurement-requests", dto);
            if (response.IsSuccessStatusCode)
            {
                StatusMessage = $"Procurement request spawned for {material.MaterialName}.";
                
                // Also post a comment about it
                await client.PostAsJsonAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/comments", 
                    new AddRequestCommentDto { Content = $"[System] Spawned procurement request for {material.MaterialName} ({material.QuantityRequired} {material.Unit})." });
                
                await LoadCommentsAsync(SelectedRequest.Id);
            }
            else
            {
                StatusMessage = $"Failed to spawn procurement: {(int)response.StatusCode}";
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
