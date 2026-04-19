using System.Security.Claims;
using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PackTracker.Api.Controllers;
using PackTracker.Api.Hubs;
using PackTracker.Application;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.ApiTests.Requests;

public class CraftingRequestsControllerTests
{
    private const string TestDiscordId = "999888777666555";
    private const string TestUsername = "testuser";

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (AppDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection) CreateSqliteDb()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options);

        db.Database.EnsureCreated();
        return (db, connection);
    }

    private static CraftingRequestsController BuildController(
        AppDbContext db,
        string discordId = TestDiscordId,
        string username = TestUsername)
    {
        var hubMock = new Mock<IHubContext<RequestsHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        clientProxyMock.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped<IApplicationDbContext>(_ => db);
        services.AddScoped<ICurrentUserService>(_ => new TestCurrentUserService(discordId, username));
        services.AddScoped<ICraftingWorkflowNotifier>(_ => new TestCraftingWorkflowNotifier(hubMock.Object));
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var controller = new CraftingRequestsController(
            mediator,
            NullLogger<CraftingRequestsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, discordId),
                    new Claim(ClaimTypes.Name, username)
                }, "Test"))
            }
        };

        return controller;
    }

    private static async Task<Profile> SeedProfileAsync(
        AppDbContext db,
        string discordId,
        string username,
        string? displayName = null)
    {
        var profile = new Profile
        {
            DiscordId = discordId,
            Username = username,
            DiscordDisplayName = displayName
        };

        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    [Fact]
    public async Task GetCraftingRequests_ReturnsVisibleRequests_WithRecipeMaterials()
    {
        var db = CreateDb();
        var requester = await SeedProfileAsync(db, TestDiscordId, TestUsername, "Requester");
        var otherUser = await SeedProfileAsync(db, "111222333444555", "other-user");

        var blueprint = new Blueprint
        {
            BlueprintName = "Laser Rifle Blueprint",
            CraftedItemName = "Laser Rifle",
            Category = "Weapon",
            Slug = "laser-rifle-blueprint"
        };

        var recipe = new BlueprintRecipe
        {
            BlueprintId = blueprint.Id,
            OutputQuantity = 1
        };

        var material = new Material
        {
            Name = "Titanium",
            Slug = "titanium",
            MaterialType = "Metal",
            Tier = "T1",
            SourceType = MaterialSourceType.Mined
        };

        var recipeMaterial = new BlueprintRecipeMaterial
        {
            BlueprintRecipeId = recipe.Id,
            MaterialId = material.Id,
            QuantityRequired = 3,
            Unit = "SCU",
            IsOptional = true,
            IsIntermediateCraftable = true
        };

        var visibleRequest = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = requester.Id,
            Status = RequestStatus.Open,
            Priority = RequestPriority.High,
            MaterialSupplyMode = MaterialSupplyMode.RequesterWillSupply,
            RequesterTimeZoneDisplayName = "Eastern Daylight Time",
            RequesterUtcOffsetMinutes = -240
        };

        var hiddenRequest = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = otherUser.Id,
            Status = RequestStatus.Accepted
        };

        db.AddRange(blueprint, recipe, material, recipeMaterial, visibleRequest, hiddenRequest);
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GetCraftingRequests(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<CraftingRequestListItemDto>>(ok.Value);

        var request = Assert.Single(list);
        Assert.Equal(visibleRequest.Id, request.Id);
        Assert.Equal("Laser Rifle", request.BlueprintName);
        Assert.Equal("Requester", request.RequesterDisplayName);
        Assert.Equal("RequesterWillSupply", request.MaterialSupplyMode);
        Assert.Equal("Eastern Daylight Time", request.RequesterTimeZoneDisplayName);
        Assert.Equal(-240, request.RequesterUtcOffsetMinutes);

        var materialDto = Assert.Single(request.Materials);
        Assert.Equal(material.Id, materialDto.MaterialId);
        Assert.Equal("Titanium", materialDto.MaterialName);
        Assert.Equal("Metal", materialDto.MaterialType);
        Assert.Equal("T1", materialDto.Tier);
        Assert.Equal(3, materialDto.QuantityRequired);
        Assert.Equal("SCU", materialDto.Unit);
        Assert.Equal("Mined", materialDto.SourceType);
        Assert.True(materialDto.IsOptional);
        Assert.True(materialDto.IsIntermediateCraftable);
    }

    [Fact]
    public async Task GetCraftingRequests_ReturnsAssignedRequest_ForAssignedCrafter()
    {
        var db = CreateDb();
        var requester = await SeedProfileAsync(db, "111222333444555", "requester");
        var assignedCrafter = await SeedProfileAsync(db, TestDiscordId, TestUsername);

        var blueprint = new Blueprint
        {
            BlueprintName = "Armor Blueprint",
            CraftedItemName = "Armor",
            Category = "Armor",
            Slug = "armor-blueprint"
        };

        var assignedRequest = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = requester.Id,
            AssignedCrafterProfileId = assignedCrafter.Id,
            Status = RequestStatus.Accepted
        };

        db.AddRange(blueprint, assignedRequest);
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GetCraftingRequests(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<CraftingRequestListItemDto>>(ok.Value);

        var request = Assert.Single(list);
        Assert.Equal(assignedRequest.Id, request.Id);
        Assert.Equal(TestUsername, request.AssignedCrafterUsername);
    }

    [Fact]
    public async Task GetCraftingRequests_LoadTest_HandlesHundredsOfVisibleRequestsWithinBudget()
    {
        var (db, connection) = CreateSqliteDb();
        await using var _ = db;
        await using var __ = connection;

        var requester = await SeedProfileAsync(db, TestDiscordId, TestUsername, "Requester");
        var blueprint = new Blueprint
        {
            BlueprintName = "Mining Laser Blueprint",
            CraftedItemName = "Mining Laser",
            Category = "Tool",
            Slug = "mining-laser-blueprint"
        };

        var recipe = new BlueprintRecipe
        {
            BlueprintId = blueprint.Id,
            OutputQuantity = 1
        };

        var materials = new[]
        {
            new Material { Name = "Iron", Slug = "iron", MaterialType = "Metal", Tier = "T1", SourceType = MaterialSourceType.Mined },
            new Material { Name = "Copper", Slug = "copper", MaterialType = "Metal", Tier = "T1", SourceType = MaterialSourceType.Mined },
            new Material { Name = "Quartz", Slug = "quartz", MaterialType = "Mineral", Tier = "T1", SourceType = MaterialSourceType.Mined }
        };

        db.AddRange(blueprint, recipe);
        db.Materials.AddRange(materials);
        await db.SaveChangesAsync();

        db.BlueprintRecipeMaterials.AddRange(
            new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = recipe.Id,
                MaterialId = materials[0].Id,
                QuantityRequired = 2,
                Unit = "SCU"
            },
            new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = recipe.Id,
                MaterialId = materials[1].Id,
                QuantityRequired = 4,
                Unit = "SCU"
            },
            new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = recipe.Id,
                MaterialId = materials[2].Id,
                QuantityRequired = 1,
                Unit = "SCU"
            });

        var requests = Enumerable.Range(0, 125)
            .Select(index => new CraftingRequest
            {
                BlueprintId = blueprint.Id,
                RequesterProfileId = requester.Id,
                Status = RequestStatus.Open,
                Priority = RequestPriority.Normal,
                MaterialSupplyMode = MaterialSupplyMode.RequesterWillSupply,
                CreatedAt = DateTime.UtcNow.AddMinutes(-index)
            })
            .ToArray();

        db.CraftingRequests.AddRange(requests);
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var stopwatch = Stopwatch.StartNew();
        var result = await controller.GetCraftingRequests(CancellationToken.None);
        stopwatch.Stop();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<CraftingRequestListItemDto>>(ok.Value);

        Assert.Equal(125, list.Count);
        Assert.All(list, request => Assert.Equal(3, request.Materials.Count));
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(3),
            $"Expected 125 crafting requests to load within 3 seconds, but took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
    }

    [Fact]
    public async Task GetLiveChat_ReturnsOnlyPersistedLiveChatEntries()
    {
        var db = CreateDb();
        var requester = await SeedProfileAsync(db, TestDiscordId, TestUsername, "Requester");

        var blueprint = new Blueprint
        {
            BlueprintName = "Shield Blueprint",
            CraftedItemName = "Shield",
            Category = "Defense",
            Slug = "shield-blueprint"
        };

        var request = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = requester.Id,
            Status = RequestStatus.Open
        };

        db.AddRange(blueprint, request);
        db.RequestComments.AddRange(
            new RequestComment
            {
                RequestId = request.Id,
                AuthorProfileId = requester.Id,
                Content = "Regular note",
                IsLiveChat = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new RequestComment
            {
                RequestId = request.Id,
                AuthorProfileId = requester.Id,
                Content = "Live ping",
                IsLiveChat = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GetLiveChat(request.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var chat = Assert.IsAssignableFrom<IReadOnlyList<RequestCommentDto>>(ok.Value);
        var message = Assert.Single(chat);
        Assert.Equal("Live ping", message.Content);
    }

    [Fact]
    public async Task DeleteCraftingRequest_AsCreator_SoftCancelsRequest()
    {
        var (db, connection) = CreateSqliteDb();
        await using var _ = db;
        await using var __ = connection;

        var requester = await SeedProfileAsync(db, TestDiscordId, TestUsername, "Requester");

        var blueprint = new Blueprint
        {
            BlueprintName = "FS-9 LMG Blueprint",
            CraftedItemName = "FS-9 LMG",
            Category = "Weapon",
            Slug = "fs-9-lmg-blueprint"
        };

        var request = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = requester.Id,
            Status = RequestStatus.Open,
            Priority = RequestPriority.Normal
        };

        db.AddRange(blueprint, request);
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.DeleteCraftingRequest(request.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        db.ChangeTracker.Clear();
        var updated = await db.CraftingRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == request.Id);
        Assert.Equal(RequestStatus.Cancelled, updated.Status);
    }

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
        private readonly IHubContext<RequestsHub> _hub;

        public TestCraftingWorkflowNotifier(IHubContext<RequestsHub> hub)
        {
            _hub = hub;
        }

        public Task NotifyAsync(string eventName, Guid requestId, CancellationToken cancellationToken) =>
            _hub.Clients.All.SendAsync(eventName, requestId, cancellationToken);
    }
}
