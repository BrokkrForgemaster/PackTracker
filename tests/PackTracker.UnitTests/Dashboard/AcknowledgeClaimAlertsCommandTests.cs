using Microsoft.EntityFrameworkCore;
using Moq;
using PackTracker.Application.Dashboard.Commands.AcknowledgeClaimAlerts;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Dashboard;

public class AcknowledgeClaimAlertsCommandTests
{
    [Fact]
    public async Task Handle_PersistsAcknowledgedClaimCounts_OnCurrentProfile()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        db.Profiles.Add(new Profile
        {
            Id = Guid.NewGuid(),
            DiscordId = "discord-123",
            Username = "sentinel_wolf"
        });
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("discord-123");

        var sut = new AcknowledgeClaimAlertsCommandHandler(db, currentUser.Object);
        var requestId = Guid.NewGuid().ToString();

        var result = await sut.Handle(
            new AcknowledgeClaimAlertsCommand(new Dictionary<string, int> { [requestId] = 2 }),
            CancellationToken.None);

        var profile = await db.Profiles.SingleAsync();

        Assert.True(result.Success);
        Assert.True(profile.AcknowledgedClaimCounts.TryGetValue(requestId, out var count));
        Assert.Equal(2, count);
    }
}
