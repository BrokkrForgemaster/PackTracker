using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Presentation.Services;

namespace PackTracker.UnitTests.Presentation;

public class AuthTokenServiceTests
{
    [Fact]
    public async Task GetAccessTokenAsync_ReturnsExistingToken_WhenStillValid()
    {
        var settings = new AppSettings
        {
            ApiBaseUrl = "https://packtracker.example",
            JwtToken = CreateJwt(minutesUntilExpiry: 30),
            JwtRefreshToken = "refresh-token"
        };

        var settingsService = CreateSettingsService(settings);
        var factory = CreateHttpClientFactory(_ => throw new InvalidOperationException("Refresh should not be called."));
        var sut = new AuthTokenService(settingsService.Object, factory.Object, NullLogger<AuthTokenService>.Instance);

        var token = await sut.GetAccessTokenAsync();

        Assert.Equal(settings.JwtToken, token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshesAndPersistsTokens_WhenExpired()
    {
        var settings = new AppSettings
        {
            ApiBaseUrl = "https://packtracker.example",
            JwtToken = CreateJwt(minutesUntilExpiry: -5),
            JwtRefreshToken = "refresh-token"
        };

        var refreshedToken = CreateJwt(minutesUntilExpiry: 45);
        var settingsService = CreateSettingsService(settings);
        var factory = CreateHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AuthTokenService.TokenPayload(refreshedToken, "next-refresh", 3600))
        });

        var sut = new AuthTokenService(settingsService.Object, factory.Object, NullLogger<AuthTokenService>.Instance);

        var token = await sut.GetAccessTokenAsync();

        Assert.Equal(refreshedToken, token);
        Assert.Equal(refreshedToken, settings.JwtToken);
        Assert.Equal("next-refresh", settings.JwtRefreshToken);
    }

    private static Mock<ISettingsService> CreateSettingsService(AppSettings settings)
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(x => x.GetSettings()).Returns(settings);
        settingsService
            .Setup(x => x.UpdateSettingsAsync(It.IsAny<Action<AppSettings>>()))
            .Returns<Action<AppSettings>>(apply =>
            {
                apply(settings);
                return Task.CompletedTask;
            });
        return settingsService;
    }

    private static Mock<IHttpClientFactory> CreateHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new StubHandler(handler), disposeHandler: true));
        return factory;
    }

    private static string CreateJwt(int minutesUntilExpiry)
    {
        var expires = DateTime.UtcNow.AddMinutes(minutesUntilExpiry).ToUniversalTime();
        var payload = $$"""{"exp":{{new DateTimeOffset(expires).ToUnixTimeSeconds()}}}""";
        return $"eyJhbGciOiJub25lIn0.{Base64UrlEncode(payload)}.";
    }

    private static string Base64UrlEncode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
