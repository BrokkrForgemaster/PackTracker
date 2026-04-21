using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;

namespace PackTracker.UnitTests.Infrastructure;

public class UexServiceTests
{
    [Fact]
    public async Task GetRoutesByCommodityAsync_UsesRequestedCommodityId_WhenCodesAreDuplicated()
    {
        var requestedUris = new List<Uri>();
        using var db = CreateDb();

        db.Commodities.AddRange(
            new Commodity
            {
                Id = 25,
                Name = "Diamond",
                Code = "DIAM",
                Slug = "diam",
                IsBuyable = true,
                IsSellable = true,
                IsVisible = true,
                IsAvailableLive = true
            },
            new Commodity
            {
                Id = 26,
                Name = "Diamond (Raw)",
                Code = "DIAM",
                Slug = "diam-raw",
                IsRaw = true,
                IsVisible = true,
                IsAvailableLive = true
            });

        await db.SaveChangesAsync();

        using var httpClient = new HttpClient(new StubHandler((request, _) =>
        {
            requestedUris.Add(request.RequestUri!);

            var query = request.RequestUri!.Query;
            var payload = query.Contains("id_commodity=25", StringComparison.Ordinal)
                ? """
                  {"status":"ok","http_code":200,"data":[{"id":1,"id_commodity":25,"origin_terminal_name":"Admin - Rat's Nest","destination_terminal_name":"Area18 TDD","price_origin":6173,"price_destination":7400,"price_roi":19.8769,"profit":240492,"commodity_name":"Diamond"}]}
                  """
                : """{"status":"ok","http_code":200,"data":[]}""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }));

        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetSettings()).Returns(new AppSettings
        {
            UexBaseUrl = "https://api.uexcorp.space/2.0"
        });

        var sut = new UexService(httpClient, NullLogger<UexService>.Instance, settings.Object, db);

        var routes = await sut.GetRoutesByCommodityAsync(25, 100, CancellationToken.None);

        var route = Assert.Single(routes);
        Assert.Equal("Diamond", route.CommodityName);
        Assert.Contains(requestedUris, uri => uri.Query.Contains("id_commodity=25", StringComparison.Ordinal));
        Assert.DoesNotContain(requestedUris, uri => uri.Query.Contains("id_commodity=26", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetCommodityPricesAsync_PrefersTradeableCommodity_WhenCodesAreDuplicated()
    {
        var requestedUris = new List<Uri>();
        using var db = CreateDb();

        db.Commodities.AddRange(
            new Commodity
            {
                Id = 25,
                Name = "Diamond",
                Code = "DIAM",
                Slug = "diam",
                IsBuyable = true,
                IsSellable = true,
                IsVisible = true,
                IsAvailableLive = true
            },
            new Commodity
            {
                Id = 26,
                Name = "Diamond (Raw)",
                Code = "DIAM",
                Slug = "diam-raw",
                IsRaw = true,
                IsVisible = true,
                IsAvailableLive = true
            });

        await db.SaveChangesAsync();

        using var httpClient = new HttpClient(new StubHandler((request, _) =>
        {
            requestedUris.Add(request.RequestUri!);

            var payload = """
                          {"status":"ok","http_code":200,"data":[{"id_commodity":25,"id_terminal":12,"terminal_name":"Area18 TDD","terminal_code":"TDA18","terminal_slug":"tdd-area18","price_buy":6173,"price_sell":7400,"scu_buy":196,"scu_sell":196,"scu_sell_stock":196,"status_buy":4,"status_sell":1,"date_added":1776737797,"date_modified":1776737797}]}
                          """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }));

        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetSettings()).Returns(new AppSettings
        {
            UexBaseUrl = "https://api.uexcorp.space/2.0"
        });

        var sut = new UexService(httpClient, NullLogger<UexService>.Instance, settings.Object, db);

        var prices = await sut.GetCommodityPricesAsync("DIAM", CancellationToken.None);

        var price = Assert.Single(prices);
        Assert.Equal(25, price.CommodityId);
        Assert.Contains(requestedUris, uri => uri.Query.Contains("id_commodity=25", StringComparison.Ordinal));
        Assert.DoesNotContain(requestedUris, uri => uri.Query.Contains("id_commodity=26", StringComparison.Ordinal));
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request, cancellationToken));
        }
    }
}
