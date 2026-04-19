using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Uex;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Presentation.Services;

/// <summary>
/// IUexService implementation that routes all calls through the remote Render API
/// instead of connecting directly to the Neon DB. Used when the app is configured
/// with a remote API base URL.
/// </summary>
public class RemoteUexService : IUexService
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly ILogger<RemoteUexService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString |
                         JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public RemoteUexService(IApiClientProvider apiClientProvider, ILogger<RemoteUexService> logger)
    {
        _apiClientProvider = apiClientProvider ?? throw new ArgumentNullException(nameof(apiClientProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<Commodity>> CommoditiesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("📦 [Remote] Fetching commodities from Render API...");
        using var client = _apiClientProvider.CreateClient();
        var response = await client.GetFromJsonAsync<ApiListResponse<Commodity>>("api/v1/uex/commodities", JsonOptions, ct);
        var result = response?.Data ?? new List<Commodity>();
        _logger.LogInformation("📦 [Remote] Received {Count} commodities.", result.Count);
        return result;
    }

    public async Task SyncCommoditiesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("🔄 [Remote] Triggering commodity sync on Render API...");
        using var client = _apiClientProvider.CreateClient();
        var resp = await client.PostAsync("api/v1/uex/sync", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<List<CommodityPrice>> GetCommodityPricesAsync(string commodityCode, CancellationToken ct)
    {
        _logger.LogInformation("💰 [Remote] Fetching prices for {Code} from Render API...", commodityCode);
        using var client = _apiClientProvider.CreateClient();
        var url = $"api/v1/uex/prices/by-code?commodityCode={Uri.EscapeDataString(commodityCode)}";
        var response = await client.GetFromJsonAsync<ApiListResponse<CommodityPrice>>(url, JsonOptions, ct);
        return response?.Data ?? new List<CommodityPrice>();
    }

    public async Task<List<UexTradeRouteDto>> GetRoutesByCommodityAsync(int commodityId, int top, CancellationToken ct)
    {
        _logger.LogInformation("🧭 [Remote] Fetching routes for commodity id {Id} from Render API...", commodityId);
        using var client = _apiClientProvider.CreateClient();
        var url = $"api/v1/uex/routes/by-id?commodityId={commodityId}&limit={top}";
        var response = await client.GetFromJsonAsync<ApiListResponse<UexTradeRouteDto>>(url, JsonOptions, ct);
        return response?.Data ?? new List<UexTradeRouteDto>();
    }

    public async Task<List<UexTradeRouteDto>> GetRoutesByCommodityCodeAsync(
        string commodityCode,
        int limit = 100,
        CancellationToken ct = default)
    {
        _logger.LogInformation("🛤️ [Remote] Fetching routes for commodity code {Code} from Render API...", commodityCode);
        using var client = _apiClientProvider.CreateClient();
        var url = $"api/v1/uex/routes/by-code?commodityCode={Uri.EscapeDataString(commodityCode)}&limit={limit}";
        var response = await client.GetFromJsonAsync<ApiListResponse<UexTradeRouteDto>>(url, JsonOptions, ct);
        return response?.Data ?? new List<UexTradeRouteDto>();
    }

    public async Task<List<UexVehicleDto>> GetVehiclesAsync(int? companyId = null, CancellationToken ct = default)
    {
        _logger.LogInformation("🚀 [Remote] Fetching vehicles from Render API...");
        using var client = _apiClientProvider.CreateClient();
        var url = companyId.HasValue
            ? $"api/v1/uex/vehicles?companyId={companyId}"
            : "api/v1/uex/vehicles";
        var response = await client.GetFromJsonAsync<ApiListResponse<UexVehicleDto>>(url, JsonOptions, ct);
        return response?.Data ?? new List<UexVehicleDto>();
    }

    private sealed class ApiListResponse<T>
    {
        [JsonPropertyName("data")] public List<T>? Data { get; set; }
        [JsonPropertyName("success")] public bool Success { get; set; }
    }
}
