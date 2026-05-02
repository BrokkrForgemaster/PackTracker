using Microsoft.EntityFrameworkCore;
using Moq;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.Queries.GetAdminAssistanceRequestDetail;
using PackTracker.Application.Admin.Queries.GetAdminCraftingRequestDetail;
using PackTracker.Application.Admin.Queries.GetAdminProcurementRequestDetail;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Admin;

public sealed class AdminRequestDetailQueryTests
{
    [Fact]
    public async Task AssistanceDetail_ReturnsFullDetail_WithClaims()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("req-1", "Alice");
        var assignee = CreateProfile("asgn-1", "Bob");
        var claimer = CreateProfile("claim-1", "Charlie");

        var request = new AssistanceRequest
        {
            Title = "Escort to Pyro",
            Description = "Need a wing escort through Pyro gate.",
            CreatedByProfileId = requester.Id,
            AssignedToProfileId = assignee.Id,
            Status = RequestStatus.Completed,
            Priority = RequestPriority.High,
            MeetingLocation = "Stanton - Area18",
            RewardOffered = "50k aUEC",
            QuantityNeeded = null,
            IsPinned = true,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-3),
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };

        var claim = new RequestClaim
        {
            RequestType = "Assistance",
            RequestId = request.Id,
            ProfileId = claimer.Id,
            ClaimedAt = DateTime.UtcNow.AddHours(-5)
        };

        db.AddRange(requester, assignee, claimer, request, claim);
        await db.SaveChangesAsync();

        var handler = new GetAdminAssistanceRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminAssistanceRequestDetailQuery(request.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Assistance", result.RequestType);
        Assert.Equal("Escort to Pyro", result.Title);
        Assert.Equal("Need a wing escort through Pyro gate.", result.Description);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("High", result.Priority);
        Assert.True(result.IsPinned);
        Assert.Equal("Alice", result.RequesterDisplayName);
        Assert.Equal("Bob", result.AssigneeDisplayName);
        Assert.Equal("Stanton - Area18", result.Location);
        Assert.Equal("50k aUEC", result.RewardOffered);
        Assert.Null(result.RefusalReason);
        Assert.Null(result.QuantityRequested);
        Assert.Null(result.QuantityDelivered);
        Assert.Null(result.MinimumQuality);
        Assert.Null(result.MaterialSupplyMode);
        Assert.NotNull(result.ClosedAt);
        Assert.Single(result.Claims);
        Assert.Equal("Charlie", result.Claims[0].DisplayName);
    }

    [Fact]
    public async Task AssistanceDetail_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var handler = new GetAdminAssistanceRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminAssistanceRequestDetailQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AssistanceDetail_FallsBackToClaimForAssignee_WhenNoDirectAssignment()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("req-2", "Alice");
        var claimer = CreateProfile("claim-2", "Dave");

        var request = new AssistanceRequest
        {
            Title = "Cargo run",
            CreatedByProfileId = requester.Id,
            AssignedToProfileId = null,
            Status = RequestStatus.Cancelled,
            Priority = RequestPriority.Normal,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };

        var claim = new RequestClaim
        {
            RequestType = "Assistance",
            RequestId = request.Id,
            ProfileId = claimer.Id,
            ClaimedAt = DateTime.UtcNow.AddHours(-2)
        };

        db.AddRange(requester, claimer, request, claim);
        await db.SaveChangesAsync();

        var handler = new GetAdminAssistanceRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminAssistanceRequestDetailQuery(request.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Dave", result.AssigneeDisplayName);
        Assert.Single(result.Claims);
    }

    [Fact]
    public async Task CraftingDetail_ReturnsFullDetail_WithClaims()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("req-3", "Eve");
        var crafter = CreateProfile("craft-1", "Frank");
        var blueprint = new Blueprint
        {
            BlueprintName = "Devastator Shotgun Blueprint",
            CraftedItemName = "Devastator Shotgun",
            Category = "Weapon",
            Slug = "devastator-shotgun"
        };

        var request = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = requester.Id,
            AssignedCrafterProfileId = crafter.Id,
            ItemName = "Devastator Shotgun",
            Notes = "Please craft at max quality.",
            DeliveryLocation = "Lorville - Hurston",
            RewardOffered = "100k aUEC",
            RefusalReason = null,
            Status = RequestStatus.Completed,
            Priority = RequestPriority.Critical,
            QuantityRequested = 2,
            MinimumQuality = 4,
            MaterialSupplyMode = MaterialSupplyMode.RequesterWillSupply,
            IsPinned = false,
            CreatedAt = DateTime.UtcNow.AddDays(-4),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-2)
        };

        db.AddRange(requester, crafter, blueprint, request);
        await db.SaveChangesAsync();

        var handler = new GetAdminCraftingRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminCraftingRequestDetailQuery(request.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Crafting", result.RequestType);
        Assert.Equal("Devastator Shotgun", result.Title);
        Assert.Equal("Please craft at max quality.", result.Description);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Critical", result.Priority);
        Assert.Equal("Eve", result.RequesterDisplayName);
        Assert.Equal("Frank", result.AssigneeDisplayName);
        Assert.Equal("Lorville - Hurston", result.Location);
        Assert.Equal("100k aUEC", result.RewardOffered);
        Assert.Null(result.RefusalReason);
        Assert.Equal(2m, result.QuantityRequested);
        Assert.Null(result.QuantityDelivered);
        Assert.Equal(4, result.MinimumQuality);
        Assert.Equal("RequesterWillSupply", result.MaterialSupplyMode);
        Assert.NotNull(result.ClosedAt);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public async Task CraftingDetail_PopulatesRefusalReason_WhenRefused()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("req-4", "Grace");
        var blueprint = new Blueprint
        {
            BlueprintName = "Rifle Blueprint",
            CraftedItemName = "Rifle",
            Category = "Weapon",
            Slug = "rifle"
        };

        var request = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = requester.Id,
            ItemName = "Rifle",
            RefusalReason = "Insufficient materials available.",
            Status = RequestStatus.Refused,
            Priority = RequestPriority.Low,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };

        db.AddRange(requester, blueprint, request);
        await db.SaveChangesAsync();

        var handler = new GetAdminCraftingRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminCraftingRequestDetailQuery(request.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Refused", result.Status);
        Assert.Equal("Insufficient materials available.", result.RefusalReason);
    }

    [Fact]
    public async Task CraftingDetail_FallsBackToBlueprintName_WhenItemNameNull()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("req-5", "Heidi");
        var blueprint = new Blueprint
        {
            BlueprintName = "FS-9 Blueprint",
            CraftedItemName = "FS-9",
            Category = "Weapon",
            Slug = "fs9"
        };

        var request = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = requester.Id,
            ItemName = null,
            Status = RequestStatus.Cancelled,
            Priority = RequestPriority.Normal,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };

        db.AddRange(requester, blueprint, request);
        await db.SaveChangesAsync();

        var handler = new GetAdminCraftingRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminCraftingRequestDetailQuery(request.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("FS-9 Blueprint", result.Title);
    }

    [Fact]
    public async Task CraftingDetail_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var handler = new GetAdminCraftingRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminCraftingRequestDetailQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcurementDetail_ReturnsFullDetail_WithClaims()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("req-6", "Ivan");
        var assignee = CreateProfile("asgn-2", "Judy");
        var claimer = CreateProfile("claim-3", "Karl");
        var material = new Material
        {
            Name = "Titanium",
            Slug = "titanium",
            MaterialType = "Metal",
            Tier = "T2",
            SourceType = MaterialSourceType.Mined
        };

        var request = new MaterialProcurementRequest
        {
            MaterialId = material.Id,
            RequesterProfileId = requester.Id,
            AssignedToProfileId = assignee.Id,
            Notes = "Raw form preferred.",
            DeliveryLocation = "New Babbage - microTech",
            RewardOffered = "75k aUEC",
            Status = RequestStatus.Completed,
            Priority = RequestPriority.High,
            QuantityRequested = 50m,
            QuantityDelivered = 50m,
            MinimumQuality = 3,
            IsPinned = false,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddHours(-4),
            CompletedAt = DateTime.UtcNow.AddHours(-2)
        };

        var claim = new RequestClaim
        {
            RequestType = "Procurement",
            RequestId = request.Id,
            ProfileId = claimer.Id,
            ClaimedAt = DateTime.UtcNow.AddDays(-2)
        };

        db.AddRange(requester, assignee, claimer, material, request, claim);
        await db.SaveChangesAsync();

        var handler = new GetAdminProcurementRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminProcurementRequestDetailQuery(request.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Procurement", result.RequestType);
        Assert.Equal("Procure: Titanium", result.Title);
        Assert.Equal("Raw form preferred.", result.Description);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("High", result.Priority);
        Assert.Equal("Ivan", result.RequesterDisplayName);
        Assert.Equal("Judy", result.AssigneeDisplayName);
        Assert.Equal("New Babbage - microTech", result.Location);
        Assert.Equal("75k aUEC", result.RewardOffered);
        Assert.Null(result.RefusalReason);
        Assert.Null(result.MaterialSupplyMode);
        Assert.Equal(50m, result.QuantityRequested);
        Assert.Equal(50m, result.QuantityDelivered);
        Assert.Equal(3, result.MinimumQuality);
        Assert.NotNull(result.ClosedAt);
        Assert.Single(result.Claims);
        Assert.Equal("Karl", result.Claims[0].DisplayName);
    }

    [Fact]
    public async Task ProcurementDetail_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var handler = new GetAdminProcurementRequestDetailQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminProcurementRequestDetailQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static Profile CreateProfile(string discordId, string displayName) =>
        new()
        {
            DiscordId = discordId,
            Username = displayName.Replace(" ", string.Empty),
            DiscordDisplayName = displayName,
            Discriminator = "0001"
        };

    private static Mock<IAuthorizationService> CreateAuthorization()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(x => x.RequirePermissionAsync(AdminPermissions.DashboardView, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return authorization;
    }
}
