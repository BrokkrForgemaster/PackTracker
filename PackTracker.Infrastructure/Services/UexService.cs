using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Uex;
using PackTracker.Application.DTOS.Uex;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Domain.Entities;

public class UexService : IUexService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UexService> _logger;
    private readonly ISettingsService _settings;
    private readonly AppDbContext _db;

    public UexService(HttpClient httpClient, ILogger<UexService> logger, ISettingsService settings , AppDbContext db)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
        _db = db;

        var uexSettings = _settings.GetSettings();
        var baseUrl = uexSettings.UexBaseUrl;
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        _httpClient.BaseAddress = new Uri(baseUrl);

        var apiKey = uexSettings.UexCorpApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    public async Task SyncCommoditiesAsync(CancellationToken ct)
    {
        _logger.LogInformation("🔄 Fetching commodities from UEX…");

        try
        {
            var response = await _httpClient.GetAsync("commodities", ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("UEX raw response: {Raw}", raw);

            response.EnsureSuccessStatusCode();

            var wrapper = JsonSerializer.Deserialize<UexCommodityResponse>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (wrapper == null || wrapper.Data.Count == 0)
            {
                _logger.LogWarning("⚠️ No commodities returned from UEX.");
                return;
            }

            _logger.LogInformation("✅ Retrieved {Count} commodities from UEX.", wrapper.Data.Count);

            foreach (var dto in wrapper.Data)
            {
                var entity = await _db.Commodities
                    .FirstOrDefaultAsync(c => c.Code == dto.Code, ct);

                if (entity == null)
                {
                    entity = new Commodity
                    {
                        Name = dto.Name,
                        Code = dto.Code,
                        Slug = dto.Name.ToLower().Replace(" ", "-"),
                        Kind = dto.Kind,
                        WeightScu = dto.Weight_Scu.HasValue ? (int)Math.Round(dto.Weight_Scu.Value) : null,
                        IsAvailable = dto.Is_Available == 1,
                        IsSellable = dto.Is_Sellable == 1,
                        IsBuyable = dto.Is_Buyable == 1,
                        IsIllegal = dto.Is_Illegal == 1,
                        Wiki = dto.Wiki,
                        DateAdded = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Added).UtcDateTime,
                        DateModified = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Modified).UtcDateTime
                    };
                    _db.Commodities.Add(entity);
                }
                else
                {
                    entity.Name = dto.Name;
                    entity.Kind = dto.Kind;
                    entity.WeightScu = dto.Weight_Scu.HasValue ? (int)Math.Round(dto.Weight_Scu.Value) : null;
                    entity.IsAvailable = dto.Is_Available == 1;
                    entity.IsSellable = dto.Is_Sellable == 1;
                    entity.IsBuyable = dto.Is_Buyable == 1;
                    entity.IsIllegal = dto.Is_Illegal == 1;
                    entity.Wiki = dto.Wiki;
                    entity.DateModified = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Modified).UtcDateTime;
                    _db.Commodities.Update(entity);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("💾 Saved {Count} commodities to database.", wrapper.Data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching commodities from UEX.");
            throw;
        }
    }


    public async Task SyncCommodityPricesAsync(CancellationToken ct)
    {
        _logger.LogInformation("🔄 Fetching commodity prices from UEX…");

        try
        {
            var response = await _httpClient.GetAsync("commodities_prices_all", ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("UEX raw prices response: {Raw}", raw);

            response.EnsureSuccessStatusCode();

            var wrapper = JsonSerializer.Deserialize<UexCommodityPriceResponse>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (wrapper == null || wrapper.Data.Count == 0)
            {
                _logger.LogWarning("⚠️ No commodity prices returned from UEX.");
                return;
            }

            _logger.LogInformation("✅ Retrieved {Count} commodity prices from UEX.", wrapper.Data.Count);

            foreach (var dto in wrapper.Data)
            {
                var entity = await _db.CommodityPrices
                    .FirstOrDefaultAsync(c => c.CommodityId == dto.Id_Commodity && c.TerminalId == dto.Id_Terminal, ct);

                if (entity == null)
                {
                    entity = new CommodityPrice
                    {
                        CommodityId = dto.Id_Commodity,
                        TerminalId = dto.Id_Terminal,
                        TerminalName = dto.Terminal_Name,
                        PriceBuy = dto.Price_Buy,
                        PriceBuyAvg = dto.Price_Buy_Avg,
                        PriceSell = dto.Price_Sell,
                        PriceSellAvg = dto.Price_Sell_Avg,
                        ScuBuy = dto.Scu_Buy,
                        ScuBuyAvg = dto.Scu_Buy_Avg,
                        ScuSellStock = dto.Scu_Sell_Stock,
                        ScuSellStockAvg = dto.Scu_Sell_Stock_Avg,
                        ScuSell = dto.Scu_Sell,
                        ScuSellAvg = dto.Scu_Sell_Avg,
                        StatusBuy = dto.Status_Buy,
                        StatusSell = dto.Status_Sell,
                        DateAdded = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Added).UtcDateTime,
                        DateModified = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Modified).UtcDateTime
                    };
                    _db.CommodityPrices.Add(entity);
                }
                else
                {
                    entity.TerminalName = dto.Terminal_Name;
                    entity.PriceBuy = dto.Price_Buy;
                    entity.PriceBuyAvg = dto.Price_Buy_Avg;
                    entity.PriceSell = dto.Price_Sell;
                    entity.PriceSellAvg = dto.Price_Sell_Avg;
                    entity.ScuBuy = dto.Scu_Buy;
                    entity.ScuBuyAvg = dto.Scu_Buy_Avg;
                    entity.ScuSellStock = dto.Scu_Sell_Stock;
                    entity.ScuSellStockAvg = dto.Scu_Sell_Stock_Avg;
                    entity.ScuSell = dto.Scu_Sell;
                    entity.ScuSellAvg = dto.Scu_Sell_Avg;
                    entity.StatusBuy = dto.Status_Buy;
                    entity.StatusSell = dto.Status_Sell;
                    entity.DateModified = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Modified).UtcDateTime;
                    _db.CommodityPrices.Update(entity);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("💾 Saved {Count} commodity prices to database.", wrapper.Data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetch{name}ing commodity prices from UEX.", "ARG0");
            throw;
        }
    }

    public async Task<List<UexTradeRouteDto>> GetTopRoutesAsync(
        int originTerminalId,
        int? destinationTerminalId,
        int top = 5,
        CancellationToken ct = default)
    {
        _logger.LogInformation("🔄 Fetching trade routes from UEX… Origin={Origin}, Destination={Destination}",
            originTerminalId, destinationTerminalId);

        try
        {
            // Build query
            var endpoint = $"commodities_routes?id_terminal_origin={originTerminalId}";
            if (destinationTerminalId.HasValue)
                endpoint += $"&id_terminal_destination={destinationTerminalId.Value}";

            var response = await _httpClient.GetAsync(endpoint, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("UEX raw trade routes response: {Raw}", raw);

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(raw);

            // UEX always wraps routes in { "status": "ok", "data": [...] }
            if (!doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                _logger.LogError("❌ Unexpected JSON from UEX: {Raw}", raw);
                return new List<UexTradeRouteDto>();
            }

            var routes = JsonSerializer.Deserialize<List<UexTradeRouteDto>>(dataElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (routes == null || routes.Count == 0)
            {
                _logger.LogWarning("⚠️ No trade routes returned from UEX.");
                return new List<UexTradeRouteDto>();
            }

            // Pick Top N by ROI, fallback Profit
            var topRoutes = routes
                .OrderByDescending(r => r.PriceRoi)
                .ThenByDescending(r => r.Profit)
                .Take(top)
                .ToList();

            _logger.LogInformation("✅ Returning {Count} top trade routes.", topRoutes.Count);

            return topRoutes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching trade routes from UEX.");
            throw;
        }
    }
}