using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Uex;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using Polly;
using Polly.Extensions.Http;

namespace PackTracker.Infrastructure.Services;

public class UexService : IUexService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UexService> _logger;
    private readonly ISettingsService _settings;
    private readonly AppDbContext _db;

    private readonly Dictionary<string, int> _codeToId = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IAsyncPolicy<HttpResponseMessage> RetryPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(r => (int)r.StatusCode == 429)
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    private static readonly Dictionary<string, List<CommodityPrice>> _priceCache = new();
    private static readonly Dictionary<int, List<UexTradeRouteDto>> _routeCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString |
                         JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public UexService(HttpClient httpClient, ILogger<UexService> logger, ISettingsService settings, AppDbContext db)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
        _db = db;
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        var s = _settings.GetSettings();
        if (string.IsNullOrWhiteSpace(s.UexBaseUrl))
            throw new InvalidOperationException("UEX Base URL missing in settings.");

        _httpClient.BaseAddress = new Uri(s.UexBaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PackTracker/1.0 (+https://housewolf.io)");
        _httpClient.DefaultRequestHeaders.Remove("x-api-key");

        if (!string.IsNullOrWhiteSpace(s.UexCorpApiKey))
            _httpClient.DefaultRequestHeaders.Add("x-api-key", s.UexCorpApiKey);

        _logger.LogInformation("🌐 HttpClient configured for UEX at {BaseUrl}", _httpClient.BaseAddress);
    }

    // ============================================================
    // 📦 COMMODITIES
    // ============================================================
    public async Task<List<Commodity>> CommoditiesAsync(CancellationToken ct)
    {
        var dbCount = await _db.Commodities.CountAsync(ct);
        if (dbCount == 0)
        {
            _logger.LogInformation("📦 No commodities found in DB. Triggering initial sync from UEX...");
            await SyncCommoditiesAsync(ct);
        }

        return await _db.Commodities.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
    }

    public async Task SyncCommoditiesAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("🌐 Fetching all commodities from UEX...");
            var result = await _httpClient.GetFromJsonAsync<UexApiResponse<List<CommodityDto>>>("commodities", JsonOptions, ct);

            if (result?.Data == null || result.Data.Count == 0)
            {
                _logger.LogWarning("⚠️ No commodities returned from UEX.");
                return;
            }

            _logger.LogInformation("✅ Retrieved {Count} commodities from UEX. Syncing to DB...", result.Data.Count);

            foreach (var dto in result.Data)
            {
                var existing = await _db.Commodities.FirstOrDefaultAsync(c => c.Id == dto.Id, ct);
                if (existing != null)
                {
                    UpdateCommodityFromDto(existing, dto);
                    _db.Commodities.Update(existing);
                }
                else
                {
                    var newCommodity = new Commodity { Id = dto.Id };
                    UpdateCommodityFromDto(newCommodity, dto);
                    _db.Commodities.Add(newCommodity);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("✅ Successfully synced {Count} commodities to DB.", result.Data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error syncing commodities from UEX.");
            throw;
        }
    }

    private static void UpdateCommodityFromDto(Commodity entity, CommodityDto dto)
    {
        entity.ParentId = dto.Id_Parent;
        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Slug = dto.Slug ?? dto.Code.ToLowerInvariant();
        entity.Kind = dto.Kind;
        entity.WeightScu = (int?)dto.Weight_Scu;
        entity.IsAvailable = dto.Is_Available == 1;
        entity.IsAvailableLive = dto.Is_Available_Live == 1;
        entity.IsVisible = dto.Is_Visible == 1;
        entity.IsExtractable = dto.Is_Extractable == 1;
        entity.IsMineral = dto.Is_Mineral == 1;
        entity.IsRaw = dto.Is_Raw == 1;
        entity.IsPure = dto.Is_Pure == 1;
        entity.IsRefined = dto.Is_Refined == 1;
        entity.IsRefinable = dto.Is_Refinable == 1;
        entity.IsHarvestable = dto.Is_Harvestable == 1;
        entity.IsBuyable = dto.Is_Buyable == 1;
        entity.IsSellable = dto.Is_Sellable == 1;
        entity.IsTemporary = dto.Is_Temporary == 1;
        entity.IsIllegal = dto.Is_Illegal == 1;
        entity.IsVolatileQt = dto.Is_Volatile_Qt == 1;
        entity.IsVolatileTime = dto.Is_Volatile_Time == 1;
        entity.IsInert = dto.Is_Inert == 1;
        entity.IsExplosive = dto.Is_Explosive == 1;
        entity.IsFuel = dto.Is_Fuel == 1;
        entity.IsBuggy = dto.Is_Buggy == 1;
        entity.Wiki = dto.Wiki;
        entity.DateAdded = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Added).UtcDateTime;
        entity.DateModified = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Modified).UtcDateTime;
    }

    // ============================================================
    // 💰 PRICES
    // ============================================================
    public async Task<List<CommodityPrice>> GetCommodityPricesAsync(string commodityCode, CancellationToken ct)
    {
        try
        {
            var commodity = await _db.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == commodityCode, ct);

            if (commodity == null)
            {
                _logger.LogWarning("❌ Commodity not found for code {Code}.", commodityCode);
                return new();
            }

            var url = $"commodities_prices?id_commodity={commodity.Id}";
            _logger.LogInformation("🌐 Fetching live prices for commodity {Name} ({Code})", commodity.Name, commodity.Code);

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                _logger.LogWarning("⚠️ No data property found in response for {Code}.", commodityCode);
                return new();
            }

            var prices = JsonSerializer.Deserialize<List<CommodityPriceDto>>(data.GetRawText(), JsonOptions);

            if (prices == null || prices.Count == 0)
            {
                _logger.LogWarning("⚠️ No prices returned from UEX for {Commodity}.", commodity.Name);
                return new();
            }

            _logger.LogInformation("✅ Retrieved {Count} prices for {Commodity}.", prices.Count, commodity.Name);

            return prices.Select(dto => new CommodityPrice
            {
                CommodityId = dto.Id_Commodity,
                TerminalId = dto.Id_Terminal,
                TerminalName = dto.Terminal_Name,
                TerminalCode = dto.Terminal_Code ?? $"term_{dto.Id_Terminal}",
                TerminalSlug = dto.Terminal_Slug ?? dto.Terminal_Code ?? $"term_{dto.Id_Terminal}",
                PriceBuy = dto.Price_Buy,
                PriceSell = dto.Price_Sell,
                ScuBuy = dto.Scu_Buy,
                ScuSell = dto.Scu_Sell,
                ScuSellStock = dto.Scu_Sell_Stock,
                StatusBuy = dto.Status_Buy,
                StatusSell = dto.Status_Sell,
                DateAdded = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Added).UtcDateTime,
                DateModified = DateTimeOffset.FromUnixTimeSeconds(dto.Date_Modified).UtcDateTime
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching prices for commodity code {Code}.", commodityCode);
            return new();
        }
    }

    // ============================================================
    // 🧭 UEX ID RESOLUTION
    // ============================================================
    private async Task<int?> ResolveUexCommodityIdAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        if (_codeToId.TryGetValue(code, out var cached))
            return cached;

        _logger.LogInformation("🔎 Resolving UEX id for commodity code {Code}...", code);
        var result = await _httpClient.GetFromJsonAsync<UexApiResponse<List<UexCommodityMini>>>("commodities", JsonOptions, ct);

        foreach (var c in result?.Data ?? [])
            _codeToId[c.Code] = c.Id;

        return _codeToId.TryGetValue(code, out var id) ? id : null;
    }

    // ============================================================
    // 🚀 ROUTES (Corrected)
    // ============================================================
    public async Task<List<UexTradeRouteDto>> GetRoutesByCommodityCodeAsync(
        string commodityCode,
        int limit = 100,
        CancellationToken ct = default)
    {
        var uexId = await ResolveUexCommodityIdAsync(commodityCode, ct);
        if (uexId == null)
        {
            _logger.LogWarning("⚠️ Could not resolve UEX id for code {Code}", commodityCode);
            return new();
        }

        var url = $"commodities_routes?id_commodity={uexId}";
        _logger.LogInformation("🌐 Requesting UEX routes from {Url}", url);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            var previewLen = Math.Min(json?.Length ?? 0, 800);
            _logger.LogTrace("UEX RAW JSON (preview): {Json}", json?.Substring(0, previewLen));

            var payload = JsonSerializer.Deserialize<UexApiResponse<List<UexTradeRouteDto>>>(json!, JsonOptions);
            var routes = payload?.Data ?? new();

            foreach (var r in routes.Take(3))
            {
                _logger.LogInformation("Route: {O} -> {D} | Buy={Buy} Sell={Sell} ROI={ROI} Profit={Profit} (Commodity={C})",
                    r.OriginTerminalName, r.DestinationTerminalName, r.PriceOrigin, r.PriceDestination, r.PriceRoi, r.Profit, r.CommodityName);
            }

            var first = routes.FirstOrDefault();
            if (first is not null)
                _logger.LogInformation("✅ First Route for {C}: Origin={O}, Buy={Buy}, Sell={Sell}",
                    first.CommodityName, first.OriginTerminalName, first.PriceOrigin, first.PriceDestination);

            return routes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to fetch routes for UEX commodity code {Code}", commodityCode);
            return new();
        }
    }

    // Legacy fallback (calls the above)
    public async Task<List<UexTradeRouteDto>> GetRoutesByCommodityAsync(
        int commodityId,
        int limit = 100,
        CancellationToken ct = default)
    {
        var commodity = await _db.Commodities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commodityId, ct);
        return commodity != null
            ? await GetRoutesByCommodityCodeAsync(commodity.Code, limit, ct)
            : new();
    }
    
    public async Task<List<UexVehicleDto>> GetVehiclesAsync(int? companyId = null, CancellationToken ct = default)
    {
        try
        {
            var url = companyId.HasValue
                ? $"vehicles?id_company={companyId}"
                : "vehicles";

            _logger.LogInformation("🌐 Requesting UEX vehicles from {Url}", url);

            var json = await _httpClient.GetStringAsync(url, ct);

            var payload = JsonSerializer.Deserialize<UexApiResponse<List<UexVehicleDto>>>(json, JsonOptions);
            var vehicles = payload?.Data ?? new List<UexVehicleDto>();

            _logger.LogInformation("✅ Loaded {Count} vehicles from UEX.", vehicles.Count);

            return vehicles
                .Where(v => v.Scu > 0 && v.IsSpaceship == 1)
                .OrderByDescending(v => v.Scu)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to fetch vehicles from UEX.");
            return new List<UexVehicleDto>();
        }
    }
}

// ============================================================
// DTOs for helper methods
// ============================================================
public sealed class UexCommodityMini
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("slug")] public string Slug { get; set; } = "";
}

public sealed class UexApiResponse<T>
{
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("http_code")] public int HttpCode { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
}
