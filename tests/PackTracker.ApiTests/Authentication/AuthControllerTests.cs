using Microsoft.AspNetCore.Mvc;
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
using PackTracker.Infrastructure.Services;
using static PackTracker.Api.Controllers.AuthController;

namespace PackTracker.ApiTests.Authentication;

public class AuthControllerTests
{
    private static (AuthController controller, AppDbContext db) BuildController()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var authOptions = Options.Create(new AuthOptions
        {
            Jwt = new JwtOptions { Key = "test-key-minimum-sixteen-characters", Issuer = "TestIssuer", Audience = "TestAud", ExpiresInMinutes = 60 },
            Discord = new DiscordOptions { ClientId = "test-id", ClientSecret = "test-secret", RequiredGuildId = "guild-id" }
        });

        var jwt = new JwtTokenService(authOptions, db, NullLogger<JwtTokenService>.Instance);
        var logger = NullLogger<AuthController>.Instance;
        var profiles = new Mock<IProfileService>();
        var houseWolf = new Mock<IHouseWolfProfileService>();
        var authWorkflow = new AuthWorkflowService(
            profiles.Object,
            houseWolf.Object,
            jwt,
            db,
            NullLogger<AuthWorkflowService>.Instance);

        var controller = new AuthController(authWorkflow, logger, authOptions);
        return (controller, db);
    }

    [Fact]
    public async Task Poll_ReturnsNotFound_WhenStateUnknown()
    {
        var (controller, _) = BuildController();

        var result = await controller.Poll("unknown-state", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Poll_ReturnsOk_WhenStateExists()
    {
        var (controller, db) = BuildController();
        db.LoginStates.Add(new LoginState
        {
            ClientState = "my-state",
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        await db.SaveChangesAsync();

        var result = await controller.Poll("my-state", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ReturnsBadRequest_WhenTokenEmpty()
    {
        var (controller, _) = BuildController();

        var result = await controller.Refresh(new RefreshTokenRequest(""), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenTokenInvalid()
    {
        var (controller, _) = BuildController();

        var result = await controller.Refresh(new RefreshTokenRequest("bad-token"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ReturnsOk_WhenTokenValid()
    {
        var (controller, db) = BuildController();

        var profile = new Profile { DiscordId = "777888999", Username = "refreshuser" };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();

        var authOptions = Options.Create(new AuthOptions
        {
            Jwt = new JwtOptions { Key = "test-key-minimum-sixteen-characters", Issuer = "TestIssuer", Audience = "TestAud", ExpiresInMinutes = 60 },
            Discord = new DiscordOptions { ClientId = "test-id", ClientSecret = "test-secret", RequiredGuildId = "guild-id" }
        });
        var jwt = new JwtTokenService(authOptions, db, NullLogger<JwtTokenService>.Instance);
        var refreshToken = await jwt.GenerateRefreshTokenAsync(profile.Id, CancellationToken.None);
        var storedRefreshToken = await db.RefreshTokens.SingleAsync();
        storedRefreshToken.Profile = profile;

        var result = await controller.Refresh(new RefreshTokenRequest(refreshToken), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value;
        Assert.NotNull(value);

        var accessTokenProp = value.GetType().GetProperty("access_token");
        Assert.NotNull(accessTokenProp);
        var accessTokenValue = accessTokenProp.GetValue(value) as string;
        Assert.False(string.IsNullOrWhiteSpace(accessTokenValue));
    }
}
