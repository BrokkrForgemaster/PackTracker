using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PackTracker.Application.Dashboard.Queries.GetDashboardSummary;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Dashboard;

public sealed class GetDashboardSummaryQueryTests
{
    [Fact]
    public async Task Handle_IncludesPinnedMineAssignedAndOpenRequestsAcrossQueues()
    {
        await using var db = CreateDb();

        var currentUser = new Profile
        {
            DiscordId = "discord-current",
            Username = "sentinel",
            DiscordDisplayName = "Sentinel"
        };

        var otherUser = new Profile
        {
            DiscordId = "discord-other",
            Username = "dragon",
            DiscordDisplayName = "Dragon"
        };

        var blueprint = new Blueprint
        {
            BlueprintName = "FS-9 LMG Blueprint",
            CraftedItemName = "FS-9 LMG",
            Category = "Weapon",
            Slug = "fs9-lmg"
        };

        var material = new Material
        {
            Name = "Iron",
            Slug = "iron",
            MaterialType = "Metal",
            Tier = "T1",
            SourceType = MaterialSourceType.Mined
        };

        var pinnedAssistance = new AssistanceRequest
        {
            Title = "Pinned escort",
            CreatedByProfileId = otherUser.Id,
            Status = RequestStatus.Open,
            Priority = RequestPriority.Normal,
            IsPinned = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var myAssistance = new AssistanceRequest
        {
            Title = "My beacon",
            CreatedByProfileId = currentUser.Id,
            Status = RequestStatus.Accepted,
            Priority = RequestPriority.High,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var assignedCrafting = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = otherUser.Id,
            AssignedCrafterProfileId = currentUser.Id,
            Status = RequestStatus.Accepted,
            Priority = RequestPriority.High,
            ItemName = "FS-9 LMG",
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        };

        var craftingClaim = new RequestClaim
        {
            RequestType = "Crafting",
            RequestId = assignedCrafting.Id,
            ProfileId = currentUser.Id
        };

        var openProcurement = new MaterialProcurementRequest
        {
            MaterialId = material.Id,
            RequesterProfileId = otherUser.Id,
            Status = RequestStatus.Open,
            Priority = RequestPriority.Normal,
            QuantityRequested = 2,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20)
        };

        var hiddenOtherAcceptedProcurement = new MaterialProcurementRequest
        {
            MaterialId = material.Id,
            RequesterProfileId = otherUser.Id,
            AssignedToProfileId = otherUser.Id,
            Status = RequestStatus.Accepted,
            Priority = RequestPriority.Normal,
            QuantityRequested = 1,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        db.AddRange(
            currentUser,
            otherUser,
            blueprint,
            material,
            pinnedAssistance,
            myAssistance,
            assignedCrafting,
            craftingClaim,
            openProcurement,
            hiddenOtherAcceptedProcurement);

        await db.SaveChangesAsync();

        var handler = new GetDashboardSummaryQueryHandler(
            db,
            new TestCurrentUserService(currentUser.DiscordId, currentUser.Username),
            NullLogger<GetDashboardSummaryQueryHandler>.Instance);

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Collection(
            result!.ActiveRequests,
            first => Assert.Equal("Pinned escort", first.Title),
            second =>
            {
                Assert.Equal("FS-9 LMG", second.Title);
                Assert.Equal("Crafting", second.RequestType);
                Assert.True(second.IsAssignedToCurrentUser);
            },
            third =>
            {
                Assert.Equal("My beacon", third.Title);
                Assert.Equal("Assistance", third.RequestType);
                Assert.True(third.IsRequestedByCurrentUser);
            },
            fourth =>
            {
                Assert.Equal("Procure: Iron", fourth.Title);
                Assert.Equal("Procurement", fourth.RequestType);
                Assert.True(fourth.IsAvailableToClaim);
            });

        Assert.Collection(
            result.PersonalContext.MyActiveTasks,
            first =>
            {
                Assert.Equal("FS-9 LMG", first.Title);
                Assert.True(first.IsAssignedToCurrentUser);
            });

        Assert.Collection(
            result.PersonalContext.MyPendingRequests,
            first =>
            {
                Assert.Equal("My beacon", first.Title);
                Assert.True(first.IsRequestedByCurrentUser);
            });
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(string userId, string displayName)
        {
            UserId = userId;
            DisplayName = displayName;
        }

        public string UserId { get; }
        public string DisplayName { get; }
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => false;
    }
}
