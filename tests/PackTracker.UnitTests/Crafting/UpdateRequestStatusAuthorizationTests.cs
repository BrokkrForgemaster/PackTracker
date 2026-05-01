using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Crafting.Commands.UpdateCraftingRequestStatus;
using PackTracker.Application.Crafting.Commands.UpdateProcurementStatus;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Crafting;

public sealed class UpdateRequestStatusAuthorizationTests
{
    [Fact]
    public async Task Handle_CraftingCompleteRejected_WhenCurrentUserIsNotRequester()
    {
        await using var db = CreateDb();

        var requester = new Profile
        {
            DiscordId = "requester-1",
            Username = "requester"
        };

        var actingUser = new Profile
        {
            DiscordId = "other-user-1",
            Username = "other-user"
        };

        var request = new CraftingRequest
        {
            RequesterProfileId = requester.Id,
            Status = RequestStatus.Accepted
        };

        db.AddRange(requester, actingUser, request);
        await db.SaveChangesAsync();

        var handler = new UpdateCraftingRequestStatusCommandHandler(
            db,
            new TestCurrentUserService(actingUser.DiscordId, actingUser.Username),
            new TestCraftingWorkflowNotifier());

        var result = await handler.Handle(
            new UpdateCraftingRequestStatusCommand(request.Id, RequestStatus.Completed.ToString()),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the creator may complete this request.", result.Message);

        var updated = await db.CraftingRequests.SingleAsync();
        Assert.Equal(RequestStatus.Accepted, updated.Status);
        Assert.Null(updated.CompletedAt);
    }

    [Fact]
    public async Task Handle_ProcurementCompleteRejected_WhenCurrentUserIsNotRequester()
    {
        await using var db = CreateDb();

        var requester = new Profile
        {
            DiscordId = "requester-2",
            Username = "requester"
        };

        var actingUser = new Profile
        {
            DiscordId = "other-user-2",
            Username = "other-user"
        };

        var material = new Material
        {
            Name = "Carbon Fiber"
        };

        var request = new MaterialProcurementRequest
        {
            MaterialId = material.Id,
            RequesterProfileId = requester.Id,
            Status = RequestStatus.Accepted,
            QuantityRequested = 10m
        };

        db.AddRange(requester, actingUser, material, request);
        await db.SaveChangesAsync();

        var handler = new UpdateProcurementStatusCommandHandler(
            db,
            new TestCurrentUserService(actingUser.DiscordId, actingUser.Username),
            new TestCraftingWorkflowNotifier());

        var result = await handler.Handle(
            new UpdateProcurementStatusCommand(request.Id, RequestStatus.Completed.ToString()),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Only the requester or assignee may complete this request.", result.Message);

        var updated = await db.MaterialProcurementRequests.SingleAsync();
        Assert.Equal(RequestStatus.Accepted, updated.Status);
        Assert.Null(updated.CompletedAt);
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

        public Task NotifyClaimedAsync(string requesterDiscordId, string claimerDiscordId, string claimerDisplayName, string requesterDisplayName, Guid requestId, string requestType, string requestLabel, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
