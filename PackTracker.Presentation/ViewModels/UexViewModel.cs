using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Uex;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Presentation.ViewModels;

/// <summary>
/// ViewModel for the Trading Hub / UEX screen.
/// Loads commodities first, then loads routes for the selected commodity.
/// Ships are loaded only when needed for profit estimation.
/// </summary>
public partial class UexViewModel : ObservableObject
{
    #region Fields

    private readonly IUexService _uex;
    private readonly ILogger<UexViewModel> _logger;
    private bool _shipsLoaded;

    #endregion

    #region Observable Properties

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

    #endregion

    #region Commands

    public IAsyncRelayCommand RefreshDataCommand { get; }
    public IAsyncRelayCommand LoadShipsCommand { get; }

    #endregion

    #region Constructor

    public UexViewModel(IUexService uex, ILogger<UexViewModel> logger)
    {
        _uex = uex ?? throw new ArgumentNullException(nameof(uex));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RefreshDataCommand = new AsyncRelayCommand(LoadDataAsync);
        LoadShipsCommand = new AsyncRelayCommand(LoadShipsAsync);

        _ = LoadDataAsync();
    }

    #endregion

    #region Initial Load

    /// <summary>
    /// Loads commodities only. Ships are not loaded automatically.
    /// </summary>
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading UEX commodities...";
            _logger.LogInformation("Loading UEX commodities...");

            var dbCommodities = await _uex.CommoditiesAsync(CancellationToken.None);
            Commodities = new ObservableCollection<Commodity>(dbCommodities);

            StatusMessage = Commodities.Count == 0
                ? "No commodities available."
                : $"Loaded {Commodities.Count} commodities.";

            _logger.LogInformation("Loaded {Count} commodities from UEX/local store.", Commodities.Count);

            AllRoutes.Clear();
            TopRoutes.Clear();
            BestRoute = null;
            EstimatedProfit = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load commodities from UEX.");
            StatusMessage = $"Error loading commodities: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Ships

    /// <summary>
    /// Loads ships from UEX only when needed for profit estimation.
    /// </summary>
    private async Task LoadShipsAsync()
    {
        if (_shipsLoaded)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Loading UEX ships...";
            _logger.LogInformation("Loading UEX ships...");

            var ships = await _uex.GetVehiclesAsync(null, CancellationToken.None);
            Ships = new ObservableCollection<UexVehicleDto>(ships);
            _shipsLoaded = true;

            _logger.LogInformation("Loaded {Count} vehicles from UEX.", Ships.Count);

            StatusMessage = Ships.Count == 0
                ? "No ships available."
                : $"Loaded {Ships.Count} ships.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ships from UEX.");
            StatusMessage = $"Error loading ships: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedShipChanged(UexVehicleDto? value)
    {
        CalculateShipProfit();
    }

    partial void OnBestRouteChanged(UexTradeRouteDto? value)
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

        _logger.LogInformation(
            "Estimated profit for {Ship}: {Profit:N0} aUEC",
            SelectedShip.NameFull,
            EstimatedProfit);
    }

    #endregion

    #region Commodity Selection / Routes

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
    /// Ships are loaded on demand the first time a route is loaded.
    /// </summary>
    private async Task LoadRoutesForCommodityAsync(Commodity commodity)
    {
        try
        {
            IsLoading = true;
            StatusMessage = $"Loading routes for {commodity.Name}...";
            _logger.LogInformation(
                "Fetching routes for commodity ID {Id} ({Name})...",
                commodity.Id,
                commodity.Name);

            var routes = await _uex.GetRoutesByCommodityAsync(commodity.Id, 100, CancellationToken.None);

            if (routes == null || routes.Count == 0)
            {
                _logger.LogWarning("No routes returned for {Commodity}.", commodity.Name);

                AllRoutes.Clear();
                TopRoutes.Clear();
                BestRoute = null;
                EstimatedProfit = null;

                StatusMessage = $"No routes found for {commodity.Name}.";
                return;
            }

            var validRoutes = routes
                .Where(HasMinimumRouteData)
                .Select(NormalizeRouteMetrics)
                .ToList();

            AllRoutes = new ObservableCollection<UexTradeRouteDto>(validRoutes);

            TopRoutes = new ObservableCollection<UexTradeRouteDto>(
                validRoutes
                    .OrderByDescending(r => r.PriceRoi)
                    .ThenByDescending(r => r.Profit)
                    .Take(5));

            BestRoute = TopRoutes.FirstOrDefault();

            _logger.LogInformation(
                "Loaded {AllCount} valid routes, showing top {TopCount} for {Commodity}.",
                AllRoutes.Count,
                TopRoutes.Count,
                commodity.Name);

            StatusMessage = $"Loaded {AllRoutes.Count} trade routes for {commodity.Name}.";

            if (!_shipsLoaded)
                await LoadShipsAsync();
            else
                CalculateShipProfit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading routes for commodity {Commodity}", commodity.Name);
            StatusMessage = $"Error loading routes for {commodity.Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    private static bool HasMinimumRouteData(UexTradeRouteDto route)
    {
        return !string.IsNullOrWhiteSpace(route.OriginTerminalName) &&
               !string.IsNullOrWhiteSpace(route.DestinationTerminalName) &&
               route.PriceOrigin.HasValue &&
               route.PriceDestination.HasValue &&
               route.PriceDestination.Value > route.PriceOrigin.Value;
    }

    private static UexTradeRouteDto NormalizeRouteMetrics(UexTradeRouteDto route)
    {
        var originPrice = route.PriceOrigin ?? 0m;
        var destinationPrice = route.PriceDestination ?? 0m;
        var margin = route.PriceMargin ?? (destinationPrice - originPrice);

        decimal? roi = route.PriceRoi;
        if (!roi.HasValue && originPrice > 0)
        {
            roi = Math.Round((margin / originPrice) * 100m, 2);
        }

        return new UexTradeRouteDto
        {
            Id = route.Id,
            IdCommodity = route.IdCommodity,
            OriginTerminalName = route.OriginTerminalName,
            DestinationTerminalName = route.DestinationTerminalName,
            PriceOrigin = route.PriceOrigin,
            PriceDestination = route.PriceDestination,
            PriceMargin = margin,
            PriceRoi = roi,
            Profit = route.Profit ?? margin,
            Distance = route.Distance,
            CommodityName = route.CommodityName
        };
    }
}
