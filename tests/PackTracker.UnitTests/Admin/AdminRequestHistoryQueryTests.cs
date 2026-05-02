using Microsoft.EntityFrameworkCore;
using Moq;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.Queries.GetAdminAssistanceRequestHistory;
using PackTracker.Application.Admin.Queries.GetAdminCraftingRequestHistory;
using PackTracker.Application.Admin.Queries.GetAdminProcurementRequestHistory;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Admin;

public sealed class AdminRequestHistoryQueryTests
{
    [Fact]
    public async Task AssistanceHistory_ReturnsTerminalStatusesOnly_NewestFirst()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("requester", "Requester");
        var assignee = CreateProfile("assignee", "Assignee");

        db.AddRange(
            requester,
            assignee,
            new AssistanceRequest
            {
                Title = "Completed escort",
                CreatedByProfileId = requester.Id,
                AssignedToProfileId = assignee.Id,
                Status = RequestStatus.Completed,
                Priority = RequestPriority.High,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                CompletedAt = DateTime.UtcNow.AddHours(-1),
                IsPinned = true
            },
            new AssistanceRequest
            {
                Title = "Cancelled haul",
                CreatedByProfileId = requester.Id,
                Status = RequestStatus.Cancelled,
                Priority = RequestPriority.Normal,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new AssistanceRequest
            {
                Title = "Hidden open",
                CreatedByProfileId = requester.Id,
                Status = RequestStatus.Open,
                Priority = RequestPriority.Low,
                CreatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        var handler = new GetAdminAssistanceRequestHistoryQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminAssistanceRequestHistoryQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal("Completed escort", first.Title);
                Assert.Equal("Completed", first.Status);
                Assert.Equal("Assistance", first.RequestType);
                Assert.Equal("Assignee", first.AssigneeDisplayName);
                Assert.True(first.IsPinned);
            },
            second =>
            {
                Assert.Equal("Cancelled haul", second.Title);
                Assert.Equal("Cancelled", second.Status);
            });
    }

    [Fact]
    public async Task CraftingHistory_ReturnsTerminalStatusesOnly_NewestFirst()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("crafter-requester", "Crafter Requester");
        var assignee = CreateProfile("crafter-assignee", "Crafter Assignee");
        var blueprint = new Blueprint
        {
            BlueprintName = "Karna Rifle Blueprint",
            CraftedItemName = "Karna Rifle",
            Category = "Weapon",
            Slug = "karna-rifle"
        };

        db.AddRange(
            requester,
            assignee,
            blueprint,
            new CraftingRequest
            {
                BlueprintId = blueprint.Id,
                RequesterProfileId = requester.Id,
                AssignedCrafterProfileId = assignee.Id,
                ItemName = "Karna Rifle",
                Status = RequestStatus.Refused,
                Priority = RequestPriority.Critical,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-30)
            },
            new CraftingRequest
            {
                BlueprintId = blueprint.Id,
                RequesterProfileId = requester.Id,
                ItemName = "FS-9 LMG",
                Status = RequestStatus.Cancelled,
                Priority = RequestPriority.Normal,
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                UpdatedAt = DateTime.UtcNow.AddHours(-3)
            },
            new CraftingRequest
            {
                BlueprintId = blueprint.Id,
                RequesterProfileId = requester.Id,
                ItemName = "Hidden accepted craft",
                Status = RequestStatus.Accepted,
                Priority = RequestPriority.Low,
                CreatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        var handler = new GetAdminCraftingRequestHistoryQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminCraftingRequestHistoryQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal("Karna Rifle", first.Title);
                Assert.Equal("Refused", first.Status);
                Assert.Equal("Crafting", first.RequestType);
                Assert.Equal("Crafter Assignee", first.AssigneeDisplayName);
            },
            second =>
            {
                Assert.Equal("FS-9 LMG", second.Title);
                Assert.Equal("Cancelled", second.Status);
            });
    }

    [Fact]
    public async Task ProcurementHistory_ReturnsTerminalStatusesOnly_NewestFirst()
    {
        await using var db = CreateDbContext();
        var requester = CreateProfile("proc-requester", "Proc Requester");
        var assignee = CreateProfile("proc-assignee", "Proc Assignee");
        var material = new Material
        {
            Name = "Copper",
            Slug = "copper",
            MaterialType = "Metal",
            Tier = "T1",
            SourceType = MaterialSourceType.Mined
        };

        db.AddRange(
            requester,
            assignee,
            material,
            new MaterialProcurementRequest
            {
                MaterialId = material.Id,
                RequesterProfileId = requester.Id,
                AssignedToProfileId = assignee.Id,
                Status = RequestStatus.Completed,
                Priority = RequestPriority.High,
                QuantityRequested = 10,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddHours(-6),
                CompletedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new MaterialProcurementRequest
            {
                MaterialId = material.Id,
                RequesterProfileId = requester.Id,
                Status = RequestStatus.Refused,
                Priority = RequestPriority.Low,
                QuantityRequested = 2,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddHours(-12)
            },
            new MaterialProcurementRequest
            {
                MaterialId = material.Id,
                RequesterProfileId = requester.Id,
                Status = RequestStatus.InProgress,
                Priority = RequestPriority.Normal,
                QuantityRequested = 5,
                CreatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        var handler = new GetAdminProcurementRequestHistoryQueryHandler(db, CreateAuthorization().Object);

        var result = await handler.Handle(new GetAdminProcurementRequestHistoryQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal("Procure: Copper", first.Title);
                Assert.Equal("Completed", first.Status);
                Assert.Equal("Procurement", first.RequestType);
                Assert.Equal("Proc Assignee", first.AssigneeDisplayName);
            },
            second =>
            {
                Assert.Equal("Refused", second.Status);
            });
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
