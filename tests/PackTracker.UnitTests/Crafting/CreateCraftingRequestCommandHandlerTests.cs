using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PackTracker.Application.Crafting.Commands.CreateCraftingRequest;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Crafting;

public sealed class CreateCraftingRequestCommandHandlerTests
{
    [Fact]
    public async Task Handle_AcceptsWikiUuidAndCreatesRequestForLocalBlueprint()
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

        db.AddRange(profile, blueprint);
        await db.SaveChangesAsync();

        var handler = new CreateCraftingRequestCommandHandler(
            db,
            new TestCurrentUserService(profile.DiscordId, profile.Username),
            new TestCraftingWorkflowNotifier(),
            NullLogger<CreateCraftingRequestCommandHandler>.Instance);

        var command = new CreateCraftingRequestCommand(new CreateCraftingRequestDto
        {
            BlueprintId = wikiUuid,
            CraftedItemName = blueprint.CraftedItemName,
            QuantityRequested = 1,
            MinimumQuality = 500,
            Priority = RequestPriority.Normal,
            MaterialSupplyMode = MaterialSupplyMode.Negotiable
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);

        var request = await db.CraftingRequests.SingleAsync();
        Assert.Equal(blueprint.Id, request.BlueprintId);
        Assert.Equal(profile.Id, request.RequesterProfileId);
    }

    [Fact]
    public async Task Handle_CrafterMustSupply_HandlesMissingRecipeGracefully()
    {
        await using var db = CreateDb();

        var profile = new Profile
        {
            DiscordId = "discord-123",
            Username = "sentinel"
        };

        var blueprint = new Blueprint
        {
            BlueprintName = "FS-9 LMG Blueprint",
            CraftedItemName = "FS-9 LMG",
            Category = "Weapon",
            Slug = "fs9-lmg-blueprint"
        };

        db.AddRange(profile, blueprint);
        await db.SaveChangesAsync();

        var handler = new CreateCraftingRequestCommandHandler(
            db,
            new TestCurrentUserService(profile.DiscordId, profile.Username),
            new TestCraftingWorkflowNotifier(),
            NullLogger<CreateCraftingRequestCommandHandler>.Instance);

        var command = new CreateCraftingRequestCommand(new CreateCraftingRequestDto
        {
            BlueprintId = blueprint.Id,
            QuantityRequested = 1,
            MaterialSupplyMode = MaterialSupplyMode.CrafterMustSupply
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, await db.CraftingRequests.CountAsync());
        Assert.Equal(0, await db.MaterialProcurementRequests.CountAsync());
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

    private sealed class TestCraftingWorkflowNotifier : ICraftingWorkflowNotifier
    {
        public Task NotifyAsync(string eventName, Guid requestId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyClaimedAsync(string requesterDiscordId, string claimerDiscordId, string claimerDisplayName, string requesterDisplayName, Guid requestId, string requestType, string requestLabel, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
