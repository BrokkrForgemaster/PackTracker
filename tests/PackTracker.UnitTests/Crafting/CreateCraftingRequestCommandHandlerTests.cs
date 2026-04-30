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

    [Fact]
    public async Task Handle_LegacyCraftingSchemaFailure_FallsBackToLegacyInsert()
    {
        await using var innerDb = CreateDb();

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

        innerDb.AddRange(profile, blueprint);
        await innerDb.SaveChangesAsync();

        var db = new LegacyCraftingInsertFallbackDbContext(innerDb);

        var handler = new CreateCraftingRequestCommandHandler(
            db,
            new TestCurrentUserService(profile.DiscordId, profile.Username),
            new TestCraftingWorkflowNotifier(),
            NullLogger<CreateCraftingRequestCommandHandler>.Instance);

        var command = new CreateCraftingRequestCommand(new CreateCraftingRequestDto
        {
            BlueprintId = blueprint.Id,
            CraftedItemName = blueprint.CraftedItemName,
            QuantityRequested = 1,
            MinimumQuality = 775,
            Priority = RequestPriority.Normal,
            MaterialSupplyMode = MaterialSupplyMode.Negotiable,
            DeliveryLocation = "Will pickup",
            RewardOffered = "Negotiable",
            MaxClaims = 1
        });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(db.LegacyInsertUsed);

        var saved = await innerDb.CraftingRequests.SingleAsync();
        Assert.Equal(blueprint.Id, saved.BlueprintId);
        Assert.Equal(profile.Id, saved.RequesterProfileId);
        Assert.Equal("Will pickup", saved.DeliveryLocation);
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

    private sealed class LegacyCraftingInsertFallbackDbContext : IApplicationDbContext
    {
        private readonly AppDbContext _inner;
        private bool _thrown;

        public LegacyCraftingInsertFallbackDbContext(AppDbContext inner) => _inner = inner;

        public bool LegacyInsertUsed { get; private set; }

        public DbSet<Profile> Profiles => _inner.Profiles;
        public DbSet<AssistanceRequest> AssistanceRequests => _inner.AssistanceRequests;
        public DbSet<CraftingRequest> CraftingRequests => _inner.CraftingRequests;
        public DbSet<MaterialProcurementRequest> MaterialProcurementRequests => _inner.MaterialProcurementRequests;
        public DbSet<GuideRequest> GuideRequests => _inner.GuideRequests;
        public DbSet<RequestTicket> RequestTickets => _inner.RequestTickets;
        public DbSet<Blueprint> Blueprints => _inner.Blueprints;
        public DbSet<MemberBlueprintOwnership> MemberBlueprintOwnerships => _inner.MemberBlueprintOwnerships;
        public DbSet<BlueprintRecipe> BlueprintRecipes => _inner.BlueprintRecipes;
        public DbSet<BlueprintRecipeMaterial> BlueprintRecipeMaterials => _inner.BlueprintRecipeMaterials;
        public DbSet<RequestComment> RequestComments => _inner.RequestComments;
        public DbSet<Material> Materials => _inner.Materials;
        public DbSet<OrgInventoryItem> OrgInventoryItems => _inner.OrgInventoryItems;
        public DbSet<Commodity> Commodities => _inner.Commodities;
        public DbSet<CommodityPrice> CommodityPrices => _inner.CommodityPrices;
        public DbSet<RequestClaim> RequestClaims => _inner.RequestClaims;
        public DbSet<LobbyChatMessage> LobbyChatMessages => _inner.LobbyChatMessages;
        public DbSet<MedalDefinition> MedalDefinitions => _inner.MedalDefinitions;
        public DbSet<MedalAward> MedalAwards => _inner.MedalAwards;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_thrown)
                return _inner.SaveChangesAsync(cancellationToken);

            _thrown = true;
            _inner.ChangeTracker.Clear();
            throw new DbUpdateException(
                "42703: column \"RequesterTimeZoneDisplayName\" of relation \"CraftingRequests\" does not exist",
                new Exception("legacy schema"));
        }

        public async Task<int> ExecuteSqlInterpolatedAsync(FormattableString sql, CancellationToken cancellationToken = default)
        {
            LegacyInsertUsed = true;

            var args = sql.GetArguments();
            var craftingRequest = new CraftingRequest
            {
                Id = (Guid)args[0]!,
                BlueprintId = (Guid)args[1]!,
                RequesterProfileId = (Guid)args[2]!,
                AssignedCrafterProfileId = args[3] as Guid?,
                QuantityRequested = (int)args[4]!,
                MinimumQuality = (int)args[5]!,
                RefusalReason = args[6] as string,
                Priority = (RequestPriority)(int)args[7]!,
                Status = (RequestStatus)(int)args[8]!,
                DeliveryLocation = args[9] as string,
                RewardOffered = args[10] as string,
                RequiredBy = args[11] as DateTime?,
                Notes = args[12] as string,
                CreatedAt = (DateTime)args[13]!,
                UpdatedAt = (DateTime)args[14]!,
                CompletedAt = args[15] as DateTime?
            };

            _inner.CraftingRequests.Add(craftingRequest);
            return await _inner.SaveChangesAsync(cancellationToken);
        }
    }
}
