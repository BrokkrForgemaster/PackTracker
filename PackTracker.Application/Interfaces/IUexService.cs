using PackTracker.Application.DTOS.Uex;

namespace PackTracker.Application.Interfaces;

public interface IUexService
{
    Task SyncCommoditiesAsync(CancellationToken ct = default);
    Task SyncCommodityPricesAsync(CancellationToken ct = default);
    
    Task<List<UexTradeRouteDto>> GetTopRoutesAsync(int originTerminalId, int? destinationTerminalId, int top, CancellationToken ct);
}