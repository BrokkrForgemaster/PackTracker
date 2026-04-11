using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Moq;
using PackTracker.Api.Controllers;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;
using static PackTracker.Api.Controllers.AuthController;

namespace PackTracker.ApiTests.Authentication;

public class AuthControllerTests
{
    private static (AuthController controller, AppDbContext db, MemoryCache cache) BuildController()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var authOptions = Options.Create(new AuthOptions
        {
            Jwt = new JwtOptions { Key = "test-key-minimum-sixteen-characters", Issuer = "TestIssuer", Audience = "TestAud", ExpiresInMinutes = 60 },
            Discord = new DiscordOptions { ClientId = "test-id", ClientSecret = "test-secret", RequiredGuildId = "guild-id" }
        });

        var jwt = new JwtTokenService(authOptions, db, NullLogger<JwtTokenService>.Instance);

        var settingsMock = new Mock<ISettingsService>();
        var profileMock = new Mock<IProfileService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<AuthController>.Instance;

        var controller = new AuthController(settingsMock.Object, jwt, profileMock.Object, db, logger, cache, authOptions);
        return (controller, db, cache);
    }

    [Fact]
    public void Poll_ReturnsNotFound_WhenStateUnknown()
    {
        var (controller, _, _) = BuildController();

        var result = controller.Poll("unknown-state");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Poll_ReturnsOk_WhenStateExists()
    {
        var (controller, _, cache) = BuildController();

        cache.Set("login-state:my-state",
            new LoginTokenPayload("access", "refresh", 3600),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        var result = controller.Poll("my-state");

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ReturnsBadRequest_WhenTokenEmpty()
    {
        var (controller, _, _) = BuildController();

        var result = await controller.Refresh(new RefreshTokenRequest(""), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenTokenInvalid()
    {
        var (controller, _, _) = BuildController();

        var result = await controller.Refresh(new RefreshTokenRequest("bad-token"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ReturnsOk_WhenTokenValid()
    {
        var (controller, db, _) = BuildController();

        var profile = new Profile { DiscordId = "777888999", Username = "refreshuser" };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();

        var refreshToken = new RefreshToken
        {
            Token = "valid-refresh-token",
            UserId = profile.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            Profile = profile
        };
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        var result = await controller.Refresh(new RefreshTokenRequest("valid-refresh-token"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value;
        Assert.NotNull(value);

        var accessTokenProp = value.GetType().GetProperty("access_token");
        Assert.NotNull(accessTokenProp);
        var accessTokenValue = accessTokenProp.GetValue(value) as string;
        Assert.False(string.IsNullOrWhiteSpace(accessTokenValue));
    }
}
