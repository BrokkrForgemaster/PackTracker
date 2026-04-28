using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PackTracker.Application.Options;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;

namespace PackTracker.UnitTests.Authentication;

public class JwtTokenServiceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static JwtTokenService CreateSut(AppDbContext? db = null) =>
        new(Options.Create(new AuthOptions
        {
            Jwt = new JwtOptions
            {
                Key = "test-key-minimum-sixteen-characters",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiresInMinutes = 60
            }
        }), db ?? CreateDb(), NullLogger<JwtTokenService>.Instance);

    [Fact]
    public void Constructor_ThrowsWhenKeyTooShort()
    {
        var options = Options.Create(new AuthOptions
        {
            Jwt = new JwtOptions { Key = "short" }
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new JwtTokenService(options, CreateDb(), NullLogger<JwtTokenService>.Instance));

        Assert.NotNull(ex);
    }

    [Fact]
    public void GenerateAccessToken_ContainsDiscordIdClaim()
    {
        var sut = CreateSut();
        var profile = new Profile { DiscordId = "123456789", Username = "tester" };

        var token = sut.GenerateAccessToken(profile);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var claim = parsed.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        Assert.NotNull(claim);
        Assert.Equal(profile.DiscordId, claim.Value);
    }

    [Fact]
    public void GenerateAccessToken_ContainsUsernameClaim()
    {
        var sut = CreateSut();
        var profile = new Profile { DiscordId = "123456789", Username = "tester" };

        var token = sut.GenerateAccessToken(profile);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var claim = parsed.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        Assert.NotNull(claim);
        Assert.Equal(profile.Username, claim.Value);
    }

    [Fact]
    public void GenerateAccessToken_ContainsJtiClaim()
    {
        var sut = CreateSut();
        var profile = new Profile { DiscordId = "123456789", Username = "tester" };

        var token = sut.GenerateAccessToken(profile);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var jti = parsed.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        Assert.NotNull(jti);
        Assert.False(string.IsNullOrWhiteSpace(jti.Value));
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_PersistsToDatabase()
    {
        var db = CreateDb();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();

        await sut.GenerateRefreshTokenAsync(userId, CancellationToken.None);

        var record = await db.RefreshTokens.FirstOrDefaultAsync(r => r.UserId == userId);
        Assert.NotNull(record);
        Assert.False(record.IsRevoked);
        Assert.True(record.ExpiresAt > DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ReturnsNull_WhenTokenUnknown()
    {
        var sut = CreateSut();

        var result = await sut.ValidateRefreshTokenAsync("does-not-exist", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ReturnsNull_WhenTokenExpired()
    {
        var db = CreateDb();
        var sut = CreateSut(db);

        var userId = Guid.NewGuid();
        var rawToken = await sut.GenerateRefreshTokenAsync(userId, CancellationToken.None);
        var expiredToken = await db.RefreshTokens.FirstAsync(r => r.UserId == userId);
        expiredToken.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var result = await sut.ValidateRefreshTokenAsync(rawToken, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ReturnsProfile_WhenValid()
    {
        var db = CreateDb();
        var sut = CreateSut(db);

        var profile = new Profile { DiscordId = "999111222", Username = "validuser" };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();

        var rawToken = await sut.GenerateRefreshTokenAsync(profile.Id, CancellationToken.None);

        var result = await sut.ValidateRefreshTokenAsync(rawToken, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(profile.DiscordId, result.DiscordId);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_MarksTokenAsRevoked()
    {
        var dbName = Guid.NewGuid().ToString();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName).Options);
        var sut = CreateSut(db);

        var rawToken = await sut.GenerateRefreshTokenAsync(Guid.NewGuid(), CancellationToken.None);

        await sut.RevokeRefreshTokenAsync(rawToken, CancellationToken.None);

        var verifyDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName).Options);
        var record = await verifyDb.RefreshTokens.FirstOrDefaultAsync();
        Assert.NotNull(record);
        Assert.True(record.IsRevoked);
        Assert.NotNull(record.RevokedAt);
    }
}
