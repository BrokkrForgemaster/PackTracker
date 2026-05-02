using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PackTracker.Application.Crafting.Queries.GetCraftingRequests;
using PackTracker.Application.Crafting.Queries.GetProcurementRequests;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Crafting;

public sealed class GetCraftingAndProcurementRequestsQueryTests
{
    [Fact]
    public async Task GetCraftingRequests_IncludesOpenMineAndAssignedRequestsOnly()
    {
        await using var db = CreateDb();

        var currentUser = CreateProfile("discord-current", "sentinel", "Sentinel");
        var otherUser = CreateProfile("discord-other", "dragon", "Dragon");
        var blueprint = CreateBlueprint();

        db.AddRange(
            currentUser,
            otherUser,
            blueprint,
            new CraftingRequest
            {
                BlueprintId = blueprint.Id,
                RequesterProfileId = otherUser.Id,
                Status = RequestStatus.Open,
                Priority = RequestPriority.Normal,
                ItemName = "Open Craft",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new CraftingRequest
            {
                BlueprintId = blueprint.Id,
                RequesterProfileId = currentUser.Id,
                Status = RequestStatus.Accepted,
                Priority = RequestPriority.High,
                ItemName = "My Craft",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new CraftingRequest
            {
                BlueprintId = blueprint.Id,
                RequesterProfileId = otherUser.Id,
                AssignedCrafterProfileId = currentUser.Id,
                Status = RequestStatus.InProgress,
                Priority = RequestPriority.High,
                ItemName = "Assigned Craft",
                CreatedAt = DateTime.UtcNow.AddMinutes(-15)
            },
            new CraftingRequest
            {
                BlueprintId = blueprint.Id,
                RequesterProfileId = otherUser.Id,
                AssignedCrafterProfileId = otherUser.Id,
                Status = RequestStatus.Accepted,
                Priority = RequestPriority.Low,
                ItemName = "Hidden Craft",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            });

        await db.SaveChangesAsync();

        var handler = new GetCraftingRequestsQueryHandler(
            db,
            CreateResolver(currentUser).Object,
            NullLogger<GetCraftingRequestsQueryHandler>.Instance);

        var result = await handler.Handle(new GetCraftingRequestsQuery(), CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, x => x.BlueprintName == "Open Craft" && x.Status == RequestStatus.Open.ToString());
        Assert.Contains(result, x => x.BlueprintName == "My Craft" && x.Status == RequestStatus.Accepted.ToString());
        Assert.Contains(result, x => x.BlueprintName == "Assigned Craft" && x.Status == RequestStatus.InProgress.ToString());
        Assert.DoesNotContain(result, x => x.BlueprintName == "Hidden Craft");
    }

    [Fact]
    public async Task GetProcurementRequests_IncludesOpenMineAndAssignedRequestsOnly()
    {
        await using var db = CreateDb();

        var currentUser = CreateProfile("discord-current", "sentinel", "Sentinel");
        var otherUser = CreateProfile("discord-other", "dragon", "Dragon");
        var material = new Material
        {
            Name = "Iron",
            Slug = "iron",
            MaterialType = "Metal",
            Tier = "T1",
            SourceType = MaterialSourceType.Mined
        };

        db.AddRange(
            currentUser,
            otherUser,
            material,
            new MaterialProcurementRequest
            {
                MaterialId = material.Id,
                RequesterProfileId = otherUser.Id,
                Status = RequestStatus.Open,
                Priority = RequestPriority.Normal,
                QuantityRequested = 3,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new MaterialProcurementRequest
            {
                MaterialId = material.Id,
                RequesterProfileId = currentUser.Id,
                Status = RequestStatus.Accepted,
                Priority = RequestPriority.High,
                QuantityRequested = 2,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new MaterialProcurementRequest
            {
                MaterialId = material.Id,
                RequesterProfileId = otherUser.Id,
                AssignedToProfileId = currentUser.Id,
                Status = RequestStatus.InProgress,
                Priority = RequestPriority.High,
                QuantityRequested = 1,
                CreatedAt = DateTime.UtcNow.AddMinutes(-15)
            },
            new MaterialProcurementRequest
            {
                MaterialId = material.Id,
                RequesterProfileId = otherUser.Id,
                AssignedToProfileId = otherUser.Id,
                Status = RequestStatus.Accepted,
                Priority = RequestPriority.Low,
                QuantityRequested = 4,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            });

        await db.SaveChangesAsync();

        var handler = new GetProcurementRequestsQueryHandler(
            db,
            CreateResolver(currentUser).Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GetProcurementRequestsQueryHandler>.Instance);

        var result = await handler.Handle(new GetProcurementRequestsQuery(), CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, x => x.QuantityRequested == 4);
        Assert.Contains(result, x => x.Status == RequestStatus.Open.ToString());
        Assert.Contains(result, x => x.Status == RequestStatus.Accepted.ToString() && x.RequesterUsername == currentUser.Username);
        Assert.Contains(result, x => x.Status == RequestStatus.InProgress.ToString() && x.AssignedToUsername == currentUser.DiscordDisplayName);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Profile CreateProfile(string discordId, string username, string displayName) =>
        new()
        {
            DiscordId = discordId,
            Username = username,
            DiscordDisplayName = displayName
        };

    private static Blueprint CreateBlueprint() =>
        new()
        {
            BlueprintName = "FS-9 LMG Blueprint",
            CraftedItemName = "FS-9 LMG",
            Category = "Weapon",
            Slug = "fs9-lmg"
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
