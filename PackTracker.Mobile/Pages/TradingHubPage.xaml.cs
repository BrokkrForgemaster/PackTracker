using System.Collections.ObjectModel;
using PackTracker.Application.DTOs.Uex;
using PackTracker.Domain.Entities;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class TradingHubPage : ContentPage
{
    private readonly PackTrackerApiClient _api;
    private readonly ObservableCollection<CommodityCard> _commodities = new();
    private readonly ObservableCollection<RouteCard> _routes = new();

    public TradingHubPage(PackTrackerApiClient api)
    {
        InitializeComponent();
        _api = api;
        CommodityView.ItemsSource = _commodities;
        RoutesView.ItemsSource = _routes;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_commodities.Count == 0)
            await LoadCommoditiesAsync().ConfigureAwait(false);
    }

    private async void LoadCommoditiesButton_Clicked(object sender, EventArgs e)
    {
        await LoadCommoditiesAsync().ConfigureAwait(false);
    }

    private async void CommodityView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not CommodityCard commodity)
            return;

        await LoadRoutesAsync(commodity).ConfigureAwait(false);
    }

    private async Task LoadCommoditiesAsync()
    {
        try
        {
            StatusLabel.Text = "Loading commodities...";
            var response = await _api.GetAsync<UexListResponse<Commodity>>("api/v1/Uex/commodities").ConfigureAwait(false);
            var items = response?.data ?? [];
            var filter = CommoditySearchEntry.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                items = items
                    .Where(x => x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                             || x.Code.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _commodities.Clear();
                foreach (var commodity in items.OrderBy(x => x.Name).Take(50))
                    _commodities.Add(new CommodityCard(commodity));

                StatusLabel.Text = _commodities.Count == 0
                    ? "No commodities matched."
                    : $"Loaded {_commodities.Count} commodities.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Commodity load failed: {ex.Message}";
            });
        }
    }

    private async Task LoadRoutesAsync(CommodityCard commodity)
    {
        try
        {
            SelectedCommodityLabel.Text = $"Loading routes for {commodity.Name}...";
            var routes = await _api.GetAsync<UexListResponse<UexTradeRouteDto>>(
                $"api/v1/Uex/routes/by-code?commodityCode={Uri.EscapeDataString(commodity.Code)}&limit=10").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _routes.Clear();
                var items = routes?.data ?? [];
                foreach (var route in items)
                    _routes.Add(new RouteCard(route));

                SelectedCommodityLabel.Text = _routes.Count == 0
                    ? $"No routes found for {commodity.Name}."
                    : $"{commodity.Name} routes";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SelectedCommodityLabel.Text = $"Route load failed: {ex.Message}";
            });
        }
    }

    private sealed class CommodityCard
    {
        public CommodityCard(Commodity commodity)
        {
            Name = commodity.Name;
            Code = commodity.Code;
            Summary = $"{commodity.Code} • Buy {commodity.PriceBuy?.ToString("0.##") ?? "-"} • Sell {commodity.PriceSell?.ToString("0.##") ?? "-"}";
        }

        public string Name { get; }
        public string Code { get; }
        public string Summary { get; }
    }

    private sealed class RouteCard
    {
        public RouteCard(UexTradeRouteDto route)
        {
            Title = $"{route.OriginTerminalName ?? "Unknown"} -> {route.DestinationTerminalName ?? "Unknown"}";
            Summary = $"Margin {route.PriceMargin?.ToString("0.##") ?? "-"} • ROI {route.PriceRoi?.ToString("0.##") ?? "-"} • Profit {route.Profit?.ToString("0.##") ?? "-"}";
        }

        public string Title { get; }
        public string Summary { get; }
    }

    private sealed class UexListResponse<T>
    {
        public List<T> data { get; set; } = [];
    }
}
