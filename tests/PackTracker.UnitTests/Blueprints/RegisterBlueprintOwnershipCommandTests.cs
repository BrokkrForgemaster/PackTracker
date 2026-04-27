using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Blueprints.Commands.RegisterBlueprintOwnership;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Application.DTOs.Wiki;
using PackTracker.Application.Interfaces;
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

        var handler = new RegisterBlueprintOwnershipCommandHandler(
            db, 
            new NoOpWikiSyncService(),
            new TestCurrentUserService(profile.DiscordId, profile.Username));

        var result = await handler.Handle(
            new RegisterBlueprintOwnershipCommand(
                wikiUuid,
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

    [Fact]
    public async Task Handle_SyncsMissingBlueprintBeforeRegisteringOwnership()
    {
        await using var db = CreateDb();

        var profile = new Profile
        {
            DiscordId = "discord-123",
            Username = "sentinel"
        };

        db.Add(profile);
        await db.SaveChangesAsync();

        var wikiUuid = Guid.NewGuid();
        var handler = new RegisterBlueprintOwnershipCommandHandler(
            db,
            new TestWikiSyncService(async ct =>
            {
                db.Blueprints.Add(new Blueprint
                {
                    BlueprintName = "Arrowhead Sniper Rifle Blueprint",
                    CraftedItemName = "Arrowhead Sniper Rifle",
                    Category = "Weapon",
                    Slug = "arrowhead-sniper-rifle-blueprint",
                    WikiUuid = wikiUuid.ToString()
                });

                await db.SaveChangesAsync(ct);
                return true;
            }),
            new TestCurrentUserService(profile.DiscordId, profile.Username));

        var result = await handler.Handle(
            new RegisterBlueprintOwnershipCommand(
                wikiUuid,
                new RegisterBlueprintOwnershipRequest
                {
                    InterestType = MemberBlueprintInterestType.Owns,
                    AvailabilityStatus = "Available"
                }),
            CancellationToken.None);

        Assert.Equal(BlueprintOwnershipRegistrationStatus.Success, result.Status);
        Assert.Equal(1, await db.MemberBlueprintOwnerships.CountAsync());
        Assert.Equal(1, result.OwnerCount);
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

    private sealed class NoOpWikiSyncService : IWikiSyncService
    {
        public Task<WikiSyncResult> SyncBlueprintsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<WikiSyncResult> SyncItemsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> SyncBlueprintAsync(Guid wikiUuid, CancellationToken ct) =>
            Task.FromResult(false);
    }

    private sealed class TestWikiSyncService : IWikiSyncService
    {
        private readonly Func<CancellationToken, Task<bool>> _syncBlueprint;

        public TestWikiSyncService(Func<CancellationToken, Task<bool>> syncBlueprint)
        {
            _syncBlueprint = syncBlueprint;
        }

        public Task<WikiSyncResult> SyncBlueprintsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<WikiSyncResult> SyncItemsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> SyncBlueprintAsync(Guid wikiUuid, CancellationToken ct) => _syncBlueprint(ct);
    }
}
