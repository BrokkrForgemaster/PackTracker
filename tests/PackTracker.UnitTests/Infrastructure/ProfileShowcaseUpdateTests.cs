using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;

namespace PackTracker.UnitTests.Infrastructure;

public class ProfileShowcaseUpdateTests
{
    [Fact]
    public async Task UpdateShowcaseAsync_UpdatesOnlyRequestedProfileFields()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        db.Profiles.Add(new Profile
        {
            DiscordId = "discord-1",
            Username = "Sentinel_Wolf",
            ShowcaseEyebrow = "OLD",
            ShowcaseTagline = "OLD TAG",
            ShowcaseBio = "OLD BIO"
        });
        await db.SaveChangesAsync();

        var sut = new ProfileService(
            db,
            new Mock<IHttpClientFactory>().Object,
            new Mock<ISettingsService>().Object,
            NullLogger<ProfileService>.Instance,
            Options.Create(new AuthOptions()));

        var updated = await sut.UpdateShowcaseAsync(
            "discord-1",
            " https://cdn.housewolf.test/sentinel.png ",
            " leadership core ",
            " Hand of House Wolf ",
            " Guardian of the pack. ",
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("https://cdn.housewolf.test/sentinel.png", updated!.ShowcaseImageUrl);
        Assert.Equal("leadership core", updated.ShowcaseEyebrow);
        Assert.Equal("Hand of House Wolf", updated.ShowcaseTagline);
        Assert.Equal("Guardian of the pack.", updated.ShowcaseBio);
    }
}
