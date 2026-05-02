using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Requests.Assistance.QueryAssistanceRequests;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Requests;

public sealed class QueryAssistanceRequestsQueryTests
{
    [Fact]
    public async Task Handle_DefaultView_ShowsOnlyOpenOrgWideAndMyAcceptedOrInProgress()
    {
        await using var db = CreateDb();

        var currentUser = CreateProfile("discord-current", "sentinel");
        var otherUser = CreateProfile("discord-other", "dragon");

        var openOrgWide = CreateRequest(otherUser.Id, "Open org-wide", RequestStatus.Open, isPinned: true);
        var myAccepted = CreateRequest(currentUser.Id, "My accepted", RequestStatus.Accepted);
        var claimedInProgress = CreateRequest(otherUser.Id, "Claimed in progress", RequestStatus.InProgress);
        var otherAccepted = CreateRequest(otherUser.Id, "Other accepted", RequestStatus.Accepted);
        var otherInProgress = CreateRequest(otherUser.Id, "Other in progress", RequestStatus.InProgress);
        var myCompleted = CreateRequest(currentUser.Id, "My completed", RequestStatus.Completed);
        var myCancelled = CreateRequest(currentUser.Id, "My cancelled", RequestStatus.Cancelled);
        var myRefused = CreateRequest(currentUser.Id, "My refused", RequestStatus.Refused);

        db.AddRange(
            currentUser,
            otherUser,
            openOrgWide,
            myAccepted,
            claimedInProgress,
            otherAccepted,
            otherInProgress,
            myCompleted,
            myCancelled,
            myRefused,
            new RequestClaim
            {
                RequestId = claimedInProgress.Id,
                RequestType = "Assistance",
                ProfileId = currentUser.Id
            });

        await db.SaveChangesAsync();

        var handler = new QueryAssistanceRequestsQueryHandler(
            db,
            CreateResolver(currentUser).Object,
            NullLogger<QueryAssistanceRequestsQueryHandler>.Instance);

        var result = await handler.Handle(new QueryAssistanceRequestsQuery(null, null), CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Collection(
            result,
            first => Assert.Equal("Open org-wide", first.Title),
            second => Assert.Equal("Claimed in progress", second.Title),
            third => Assert.Equal("My accepted", third.Title));
        Assert.DoesNotContain(result, x => x.Title == "Other accepted");
        Assert.DoesNotContain(result, x => x.Title == "Other in progress");
        Assert.DoesNotContain(result, x => x.Title == "My completed");
        Assert.DoesNotContain(result, x => x.Title == "My cancelled");
        Assert.DoesNotContain(result, x => x.Title == "My refused");
    }

    [Fact]
    public async Task Handle_ClosedStatusFilter_ReturnsNoResultsForStandardUsers()
    {
        await using var db = CreateDb();

        var currentUser = CreateProfile("discord-current", "sentinel");
        var completed = CreateRequest(currentUser.Id, "My completed", RequestStatus.Completed);

        db.AddRange(currentUser, completed);
        await db.SaveChangesAsync();

        var handler = new QueryAssistanceRequestsQueryHandler(
            db,
            CreateResolver(currentUser).Object,
            NullLogger<QueryAssistanceRequestsQueryHandler>.Instance);

        var result = await handler.Handle(
            new QueryAssistanceRequestsQuery(null, RequestStatus.Completed),
            CancellationToken.None);

        Assert.Empty(result);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Profile CreateProfile(string discordId, string username) =>
        new()
        {
            DiscordId = discordId,
            Username = username,
            DiscordDisplayName = username
        };

    private static AssistanceRequest CreateRequest(Guid createdByProfileId, string title, RequestStatus status, bool isPinned = false) =>
        new()
        {
            CreatedByProfileId = createdByProfileId,
            Title = title,
            Description = $"{title} description",
            Kind = RequestKind.Other,
            Priority = RequestPriority.Normal,
            Status = status,
            IsPinned = isPinned,
            CreatedAt = DateTime.UtcNow
        };

    private static Mock<ICurrentUserProfileResolver> CreateResolver(Profile profile)
    {
        var resolver = new Mock<ICurrentUserProfileResolver>();
        resolver
            .Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserProfileContext(profile.DiscordId, profile));

        return resolver;
    }
}
