using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Blueprints.Queries.GetOwnedBlueprints;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Blueprints;

public sealed class GetOwnedBlueprintsQueryTests
{
    [Fact]
    public async Task Handle_ReturnsCurrentUsersOwnedBlueprintsWithRecipeMaterials()
    {
        await using var db = CreateDb();

        var currentProfile = new Profile
        {
            DiscordId = "discord-1",
            Username = "sentinel"
        };

        var otherProfile = new Profile
        {
            DiscordId = "discord-2",
            Username = "other"
        };

        var ownedBlueprint = new Blueprint
        {
            BlueprintName = "Atlas Mining Laser Blueprint",
            CraftedItemName = "Atlas Mining Laser",
            Category = "Mining",
            WikiUuid = Guid.NewGuid().ToString()
        };

        var otherBlueprint = new Blueprint
        {
            BlueprintName = "FS-9 LMG Blueprint",
            CraftedItemName = "FS-9 LMG",
            Category = "Weapon",
            WikiUuid = Guid.NewGuid().ToString()
        };

        var quantanium = new Material
        {
            Name = "Quantanium",
            MaterialType = "Ore",
            Tier = "T1",
            SourceType = MaterialSourceType.Mined
        };

        var corundum = new Material
        {
            Name = "Corundum",
            MaterialType = "Ore",
            Tier = "T2",
            SourceType = MaterialSourceType.Mined
        };

        var ownedRecipe = new BlueprintRecipe
        {
            BlueprintId = ownedBlueprint.Id,
            OutputQuantity = 1
        };

        var otherRecipe = new BlueprintRecipe
        {
            BlueprintId = otherBlueprint.Id,
            OutputQuantity = 1
        };

        db.AddRange(currentProfile, otherProfile, ownedBlueprint, otherBlueprint, quantanium, corundum, ownedRecipe, otherRecipe);
        db.MemberBlueprintOwnerships.AddRange(
            new MemberBlueprintOwnership
            {
                BlueprintId = ownedBlueprint.Id,
                MemberProfileId = currentProfile.Id,
                InterestType = MemberBlueprintInterestType.Owns,
                AvailabilityStatus = "Available",
                Notes = "Hangar shelf A."
            },
            new MemberBlueprintOwnership
            {
                BlueprintId = otherBlueprint.Id,
                MemberProfileId = otherProfile.Id,
                InterestType = MemberBlueprintInterestType.Owns,
                AvailabilityStatus = "Available"
            });

        db.BlueprintRecipeMaterials.AddRange(
            new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = ownedRecipe.Id,
                MaterialId = quantanium.Id,
                QuantityRequired = 12.5,
                Unit = "SCU"
            },
            new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = ownedRecipe.Id,
                MaterialId = corundum.Id,
                QuantityRequired = 6,
                Unit = "SCU"
            },
            new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = otherRecipe.Id,
                MaterialId = corundum.Id,
                QuantityRequired = 2,
                Unit = "SCU"
            });

        await db.SaveChangesAsync();

        var handler = new GetOwnedBlueprintsQueryHandler(db, new TestCurrentUserService("discord-1"));

        var result = await handler.Handle(new GetOwnedBlueprintsQuery(), CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(ownedBlueprint.Id, item.BlueprintId);
        Assert.Equal("Atlas Mining Laser", item.CraftedItemName);
        Assert.Equal("Available", item.AvailabilityStatus);
        Assert.Equal(2, item.Materials.Count);
        Assert.Contains(item.Materials, x => x.MaterialName == "Quantanium" && x.QuantityRequired == 12.5);
        Assert.Contains(item.Materials, x => x.MaterialName == "Corundum" && x.QuantityRequired == 6);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(string userId)
        {
            UserId = userId;
        }

        public string UserId { get; }
        public string DisplayName => "sentinel";
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => false;
    }
}
