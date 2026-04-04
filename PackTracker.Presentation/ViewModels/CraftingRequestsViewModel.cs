using System.Collections.ObjectModel;
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

    public CraftingRequestsViewModel(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
        StatusMessage = "Loading crafting requests...";
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing crafting requests...";

            using var client = _apiClientProvider.CreateClient();
        
            // 1. Use GetAsync instead of GetFromJsonAsync to safely inspect the result
            using var response = await client.GetAsync("api/v1/crafting/requests");

            // 2. Check for HTTP Success (200 OK)
            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Server Error: {(int)response.StatusCode} {response.ReasonPhrase}";
                return;
            }

            // 3. Verify the Content-Type is actually JSON
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != "application/json" && !(contentType?.Contains("json") ?? false))
            {
                // If we got here, we likely hit a 404 or redirect that returned HTML
                var preview = await response.Content.ReadAsStringAsync();
                StatusMessage = "API returned HTML instead of JSON. Check your ApiBaseUrl.";
                return;
            }

            // 4. Now it is safe to read the JSON
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
            // This will now only catch real connection issues (like DNS or Timeout)
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
