using Microsoft.Extensions.Logging.Abstractions;
using PackTracker.Application.DTOs.Uex;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.UnitTests.Presentation;

public class UexViewModelTests
{
    [Fact]
    public async Task SelectedCommodity_LoadsRoute_WhenRoiIsMissingButPricesArePresent()
    {
        var diamond = new Commodity
        {
            Id = 42,
            Name = "Diamond",
            Code = "diamond"
        };

        var sut = new UexViewModel(
            new StubUexService(
                commodities: [diamond],
                routesByCommodityId: new Dictionary<int, List<UexTradeRouteDto>>
                {
                    [diamond.Id] =
                    [
                        new UexTradeRouteDto
                        {
                            Id = 1,
                            IdCommodity = diamond.Id,
                            CommodityName = diamond.Name,
                            OriginTerminalName = "Shubin SAL-2",
                            DestinationTerminalName = "Area18 TDD",
                            PriceOrigin = 500m,
                            PriceDestination = 650m,
                            PriceRoi = null,
                            Profit = null
                        }
                    ]
                }),
            NullLogger<UexViewModel>.Instance);

        await WaitForAsync(() => sut.Commodities.Count == 1);

        sut.SelectedCommodity = sut.Commodities.Single();

        await WaitForAsync(() => sut.AllRoutes.Count == 1);

        var route = Assert.Single(sut.AllRoutes);
        Assert.Equal(150m, route.PriceMargin);
        Assert.Equal(30m, route.PriceRoi);
        Assert.Equal(route, sut.BestRoute);
        Assert.Single(sut.TopRoutes);
        Assert.Equal("Diamond", route.CommodityName);
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(25);
        }
    }

    private sealed class StubUexService : IUexService
    {
        private readonly List<Commodity> _commodities;
        private readonly Dictionary<int, List<UexTradeRouteDto>> _routesByCommodityId;

        public StubUexService(
            List<Commodity> commodities,
            Dictionary<int, List<UexTradeRouteDto>> routesByCommodityId)
        {
            _commodities = commodities;
            _routesByCommodityId = routesByCommodityId;
        }

        public Task<List<Commodity>> CommoditiesAsync(CancellationToken ct = default) => Task.FromResult(_commodities);

        public Task SyncCommoditiesAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<List<CommodityPrice>> GetCommodityPricesAsync(string commodityCode, CancellationToken ct)
            => Task.FromResult(new List<CommodityPrice>());

        public Task<List<UexTradeRouteDto>> GetRoutesByCommodityAsync(int commodityId, int top, CancellationToken ct)
            => Task.FromResult(_routesByCommodityId.TryGetValue(commodityId, out var routes) ? routes : new List<UexTradeRouteDto>());

        public Task<List<UexTradeRouteDto>> GetRoutesByCommodityCodeAsync(string commodityCode, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(new List<UexTradeRouteDto>());

        public Task<List<UexVehicleDto>> GetVehiclesAsync(int? companyId = null, CancellationToken ct = default)
            => Task.FromResult(new List<UexVehicleDto>());
    }
}
