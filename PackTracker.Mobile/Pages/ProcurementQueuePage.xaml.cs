using System.Collections.ObjectModel;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class ProcurementQueuePage : ContentPage
{
    private readonly PackTrackerApiClient _api;
    private readonly ObservableCollection<ProcurementCard> _items = new();

    public ProcurementQueuePage(PackTrackerApiClient api)
    {
        InitializeComponent();
        _api = api;
        ProcurementView.ItemsSource = _items;
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
            var items = await _api.GetAsync<List<MaterialProcurementRequestListItemDto>>(
                "api/v1/crafting/procurement-requests").ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _items.Clear();
                if (items is null || items.Count == 0)
                {
                    StatusLabel.Text = "No procurement requests.";
                    return;
                }

                foreach (var item in items)
                    _items.Add(new ProcurementCard(item));

                StatusLabel.Text = $"Loaded {_items.Count} procurement requests.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Procurement failed: {ex.Message}";
            });
        }
    }

    private sealed class ProcurementCard
    {
        public ProcurementCard(MaterialProcurementRequestListItemDto dto)
        {
            Title = dto.MaterialName;
            Summary = $"{dto.Status} • Qty {dto.QuantityRequested} • {dto.Priority} • {dto.RequesterDisplayName}";
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? "No notes." : dto.Notes;
        }

        public string Title { get; }
        public string Summary { get; }
        public string Notes { get; }
    }
}
