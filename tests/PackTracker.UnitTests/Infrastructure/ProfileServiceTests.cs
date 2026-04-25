using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;

namespace PackTracker.UnitTests.Infrastructure;

public class ProfileServiceTests
{
    [Fact]
    public async Task GetByNameAsync_FindsProfile_CaseInsensitively()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        db.Profiles.Add(new Profile
        {
            Id = Guid.NewGuid(),
            DiscordId = "123",
            Username = "zombierecon",
            DiscordDisplayName = "ZombieRecon"
        });
        await db.SaveChangesAsync();

        var httpClientFactory = new Mock<IHttpClientFactory>();
        var settingsService = new Mock<ISettingsService>();
        var authOptions = Options.Create(new AuthOptions());

        var sut = new ProfileService(
            db,
            httpClientFactory.Object,
            settingsService.Object,
            NullLogger<ProfileService>.Instance,
            authOptions);

        var profile = await sut.GetByNameAsync("ZombieRecon", CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal("zombierecon", profile!.Username);
    }
}
