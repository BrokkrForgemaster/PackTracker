using PackTracker.Application.DTOs.Uex;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

public interface IUexService
{
    Task<List<Commodity>> CommoditiesAsync(CancellationToken ct = default);
    Task<List<CommodityPrice>> GetCommodityPricesAsync(string commodityCode, CancellationToken ct);
    Task<List<UexTradeRouteDto>> GetRoutesByCommodityAsync(int commodityId, int top, CancellationToken ct);
    Task<List<UexTradeRouteDto>> GetRoutesByCommodityCodeAsync(
        string commodityCode,
        int limit = 100,
        CancellationToken ct = default);
    
    Task<List<UexVehicleDto>> GetVehiclesAsync(int? companyId = null, CancellationToken ct = default);


}