using System.Collections.ObjectModel;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class CraftingQueuePage : ContentPage
{
    private readonly PackTrackerApiClient _api;
    private readonly ObservableCollection<CraftingCard> _items = new();

    public CraftingQueuePage(PackTrackerApiClient api)
    {
        InitializeComponent();
        _api = api;
        CraftingView.ItemsSource = _items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync().ConfigureAwait(false);
    }

    private async void RefreshButton_Clicked(object sender, EventArgs e)
    {
        await LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        try
        {
            var items = await _api.GetAsync<List<CraftingRequestListItemDto>>("api/v1/crafting/requests").ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _items.Clear();
                if (items is null || items.Count == 0)
                {
                    StatusLabel.Text = "No crafting requests.";
                    return;
                }

                foreach (var item in items)
                    _items.Add(new CraftingCard(item));

                StatusLabel.Text = $"Loaded {_items.Count} crafting requests.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Crafting failed: {ex.Message}";
            });
        }
    }

    private sealed class CraftingCard
    {
        public CraftingCard(CraftingRequestListItemDto dto)
        {
            Title = string.IsNullOrWhiteSpace(dto.CraftedItemName) ? dto.BlueprintName : dto.CraftedItemName;
            Summary = $"{dto.Status} • Qty {dto.QuantityRequested} • {dto.Priority} • {dto.RequesterDisplayName}";
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? "No notes." : dto.Notes;
        }

        public string Title { get; }
        public string Summary { get; }
        public string Notes { get; }
    }
}
