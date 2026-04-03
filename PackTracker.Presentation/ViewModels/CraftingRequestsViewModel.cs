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
            var items = await client.GetFromJsonAsync<List<CraftingRequestListItemDto>>("api/v1/crafting/requests")
                        ?? new List<CraftingRequestListItemDto>();

            Requests.Clear();
            foreach (var item in items)
                Requests.Add(item);

            StatusMessage = Requests.Count == 0
                ? "No crafting requests yet. Create one from the Blueprint Explorer."
                : $"Loaded {Requests.Count} crafting requests.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load crafting requests: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
