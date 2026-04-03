using System.Collections.ObjectModel;
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

    public ProcurementRequestsViewModel(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
        StatusMessage = "Loading material procurement requests...";
        _ = RefreshAsync();
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
                ? "No procurement requests yet. Link them from crafting needs next."
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
}
