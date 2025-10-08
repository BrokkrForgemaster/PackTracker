using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Application.DTOs.Uex;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PackTracker.Presentation.ViewModels;

public partial class UexViewModel : ObservableObject
{
    private readonly IUexService _uex;
    private readonly ILogger<UexViewModel> _logger;

    [ObservableProperty] private ObservableCollection<Commodity> _commodities = new();
    [ObservableProperty] private Commodity? _selectedCommodity;

    [ObservableProperty] private ObservableCollection<UexTradeRouteDto> _allRoutes = new();
    [ObservableProperty] private ObservableCollection<UexTradeRouteDto> _topRoutes = new();
    [ObservableProperty] private UexTradeRouteDto? _bestRoute;

    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<UexVehicleDto> _ships = new();
    [ObservableProperty] private UexVehicleDto? _selectedShip;
    [ObservableProperty] private decimal? _estimatedProfit;

    public IAsyncRelayCommand RefreshDataCommand { get; }

    public UexViewModel(IUexService uex, ILogger<UexViewModel> logger)
    {
        _uex = uex ?? throw new ArgumentNullException(nameof(uex));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RefreshDataCommand = new AsyncRelayCommand(LoadDataAsync);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    /// <summary>
    /// Loads the list of commodities for selection.
    /// </summary>
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading UEX commodities...";
            _logger.LogInformation("🔄 Loading UEX commodities...");
            
            var ships = await _uex.GetVehiclesAsync(null, CancellationToken.None);
            Ships = new ObservableCollection<UexVehicleDto>(ships);
            _logger.LogInformation("✅ Loaded {Count} vehicles from UEX.", Ships.Count);
            var dbCommodities = await _uex.CommoditiesAsync(CancellationToken.None);
            Commodities = new ObservableCollection<Commodity>(dbCommodities);

            StatusMessage = $"✅ Loaded {Commodities.Count} commodities.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load commodities from UEX.");
            StatusMessage = $"⚠️ Error loading commodities: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    partial void OnSelectedShipChanged(UexVehicleDto? ship)
    {
        CalculateShipProfit();
    }

    partial void OnBestRouteChanged(UexTradeRouteDto? route)
    {
        CalculateShipProfit();
    }

    private void CalculateShipProfit()
    {
        if (SelectedShip is null || BestRoute is null || SelectedShip.Scu is null)
        {
            EstimatedProfit = null;
            return;
        }

        var perUnitProfit = (BestRoute.PriceDestination - BestRoute.PriceOrigin) ?? 0;
        EstimatedProfit = perUnitProfit * (decimal)SelectedShip.Scu.Value;

        _logger.LogInformation("🛳 Estimated profit for {Ship}: {Profit:N0} aUEC",
            SelectedShip.NameFull, EstimatedProfit);
    }


    /// <summary>
    /// Triggers route loading whenever the user selects a commodity.
    /// </summary>
    partial void OnSelectedCommodityChanged(Commodity? value)
    {
        if (value is not null)
            _ = LoadRoutesForCommodityAsync(value);
    }

    /// <summary>
    /// Loads all and top trade routes for the selected commodity.
    /// </summary>
    private async Task LoadRoutesForCommodityAsync(Commodity commodity)
    {
        try
        {
            IsLoading = true;
            StatusMessage = $"📦 Loading routes for {commodity.Name}...";
            _logger.LogInformation("🌐 Fetching routes for commodity ID {Id} ({Name})...", commodity.Id, commodity.Name);

            var routes = await _uex.GetRoutesByCommodityAsync(commodity.Id, 100, CancellationToken.None);
            _logger.LogInformation("First Route -> Origin={Origin} | Buy={Buy} | Sell={Sell} | ROI={ROI}",
                routes.FirstOrDefault()?.OriginTerminalName,
                routes.FirstOrDefault()?.PriceOrigin,
                routes.FirstOrDefault()?.PriceDestination,
                routes.FirstOrDefault()?.PriceRoi);
            if (routes == null || routes.Count == 0)
            {
                _logger.LogWarning("⚠️ No routes returned for {Commodity}.", commodity.Name);
                AllRoutes.Clear();
                TopRoutes.Clear();
                BestRoute = null;
                StatusMessage = $"⚠️ No routes found for {commodity.Name}.";
                return;
            }

            // ✅ Filter valid data only
            var validRoutes = routes
                .Where(r =>
                    !string.IsNullOrWhiteSpace(r.OriginTerminalName) &&
                    !string.IsNullOrWhiteSpace(r.DestinationTerminalName) &&
                    r.PriceRoi.HasValue && r.PriceOrigin.HasValue && r.PriceDestination.HasValue)
                .ToList();

            AllRoutes = new ObservableCollection<UexTradeRouteDto>(validRoutes);

            TopRoutes = new ObservableCollection<UexTradeRouteDto>(
                validRoutes.OrderByDescending(r => r.PriceRoi).ThenByDescending(r => r.Profit).Take(5)
            );

            BestRoute = TopRoutes.FirstOrDefault();

            _logger.LogInformation("✅ Loaded {AllCount} valid routes, showing top {TopCount} for {Commodity}.",
                AllRoutes.Count, TopRoutes.Count, commodity.Name);

            StatusMessage = $"✅ Loaded {AllRoutes.Count} trade routes for {commodity.Name}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error loading routes for commodity {Commodity}", commodity.Name);
            StatusMessage = $"⚠️ Error loading routes for {commodity.Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
