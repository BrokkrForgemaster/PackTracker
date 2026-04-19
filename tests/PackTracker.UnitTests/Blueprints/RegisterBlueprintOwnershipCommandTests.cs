using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Blueprints.Commands.RegisterBlueprintOwnership;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Blueprints;

public sealed class RegisterBlueprintOwnershipCommandTests
{
    [Fact]
    public async Task Handle_AcceptsWikiUuidAndUpdatesExistingOwnership()
    {
        await using var db = CreateDb();

        var profile = new Profile
        {
            DiscordId = "discord-123",
            Username = "sentinel"
        };

        var wikiUuid = Guid.NewGuid();
        var blueprint = new Blueprint
        {
            BlueprintName = "FS-9 LMG Blueprint",
            CraftedItemName = "FS-9 LMG",
            Category = "Weapon",
            Slug = "fs9-lmg-blueprint",
            WikiUuid = wikiUuid.ToString()
        };

        var existing = new MemberBlueprintOwnership
        {
            BlueprintId = blueprint.Id,
            MemberProfileId = profile.Id,
            InterestType = MemberBlueprintInterestType.Wants,
            AvailabilityStatus = "Busy"
        };

        db.AddRange(profile, blueprint, existing);
        await db.SaveChangesAsync();

        var handler = new RegisterBlueprintOwnershipCommandHandler(db);

        var result = await handler.Handle(
            new RegisterBlueprintOwnershipCommand(
                wikiUuid,
                profile.DiscordId,
                new RegisterBlueprintOwnershipRequest
                {
                    InterestType = MemberBlueprintInterestType.Owns,
                    AvailabilityStatus = "Available",
                    Notes = "Ready to craft."
                }),
            CancellationToken.None);

        Assert.Equal(BlueprintOwnershipRegistrationStatus.Success, result.Status);

        var ownership = await db.MemberBlueprintOwnerships.SingleAsync();
        Assert.Equal(existing.Id, ownership.Id);
        Assert.Equal(MemberBlueprintInterestType.Owns, ownership.InterestType);
        Assert.Equal("Available", ownership.AvailabilityStatus);
        Assert.Equal("Ready to craft.", ownership.Notes);
        Assert.Equal(1, result.OwnerCount);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
