using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Crafting.Commands.DeleteCraftingRequest;
using PackTracker.Application.Crafting.Commands.UpdateCraftingRequestStatus;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Crafting;

public sealed class LinkedProcurementCascadeTests
{
    [Fact]
    public async Task Handle_CompletingCraftingRequest_CompletesLinkedProcurementRequests()
    {
        await using var db = CreateDb();

        var requester = new Profile
        {
            DiscordId = "requester-complete",
            Username = "requester"
        };

        var request = new CraftingRequest
        {
            RequesterProfileId = requester.Id,
            Status = RequestStatus.Accepted
        };

        var material = new Material { Name = "Titanium" };
        var linkedOpen = new MaterialProcurementRequest
        {
            LinkedCraftingRequestId = request.Id,
            MaterialId = material.Id,
            RequesterProfileId = requester.Id,
            QuantityRequested = 5m,
            Status = RequestStatus.Open
        };
        var linkedAccepted = new MaterialProcurementRequest
        {
            LinkedCraftingRequestId = request.Id,
            MaterialId = material.Id,
            RequesterProfileId = requester.Id,
            QuantityRequested = 8m,
            Status = RequestStatus.Accepted
        };
        var unrelated = new MaterialProcurementRequest
        {
            MaterialId = material.Id,
            RequesterProfileId = requester.Id,
            QuantityRequested = 3m,
            Status = RequestStatus.Open
        };

        db.AddRange(requester, request, material, linkedOpen, linkedAccepted, unrelated);
        await db.SaveChangesAsync();

        var notifier = new RecordingCraftingWorkflowNotifier();
        var handler = new UpdateCraftingRequestStatusCommandHandler(
            db,
            new TestCurrentUserService(requester.DiscordId, requester.Username),
            notifier);

        var result = await handler.Handle(
            new UpdateCraftingRequestStatusCommand(request.Id, RequestStatus.Completed.ToString()),
            CancellationToken.None);

        Assert.True(result.Success);

        var updatedLinkedOpen = await db.MaterialProcurementRequests.FindAsync(linkedOpen.Id);
        var updatedLinkedAccepted = await db.MaterialProcurementRequests.FindAsync(linkedAccepted.Id);
        var updatedUnrelated = await db.MaterialProcurementRequests.FindAsync(unrelated.Id);

        Assert.Equal(RequestStatus.Completed, updatedLinkedOpen!.Status);
        Assert.NotNull(updatedLinkedOpen.CompletedAt);
        Assert.Equal(RequestStatus.Completed, updatedLinkedAccepted!.Status);
        Assert.NotNull(updatedLinkedAccepted.CompletedAt);
        Assert.Equal(RequestStatus.Open, updatedUnrelated!.Status);
        Assert.Null(updatedUnrelated.CompletedAt);

        Assert.Contains(("CraftingRequestUpdated", request.Id), notifier.Events);
        Assert.Contains(("ProcurementUpdated", linkedOpen.Id), notifier.Events);
        Assert.Contains(("ProcurementUpdated", linkedAccepted.Id), notifier.Events);
    }

    [Fact]
    public async Task Handle_DeletingCraftingRequest_CancelsLinkedProcurementRequests()
    {
        await using var db = CreateDb();

        var requester = new Profile
        {
            DiscordId = "requester-delete",
            Username = "requester"
        };

        var request = new CraftingRequest
        {
            RequesterProfileId = requester.Id,
            Status = RequestStatus.Open
        };

        var material = new Material { Name = "Copper" };
        var linkedOpen = new MaterialProcurementRequest
        {
            LinkedCraftingRequestId = request.Id,
            MaterialId = material.Id,
            RequesterProfileId = requester.Id,
            QuantityRequested = 5m,
            Status = RequestStatus.Open
        };
        var linkedCompleted = new MaterialProcurementRequest
        {
            LinkedCraftingRequestId = request.Id,
            MaterialId = material.Id,
            RequesterProfileId = requester.Id,
            QuantityRequested = 8m,
            Status = RequestStatus.Completed,
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };
        var unrelated = new MaterialProcurementRequest
        {
            MaterialId = material.Id,
            RequesterProfileId = requester.Id,
            QuantityRequested = 3m,
            Status = RequestStatus.Open
        };

        db.AddRange(requester, request, material, linkedOpen, linkedCompleted, unrelated);
        await db.SaveChangesAsync();

        var notifier = new RecordingCraftingWorkflowNotifier();
        var handler = new DeleteCraftingRequestCommandHandler(
            db,
            new TestCurrentUserService(requester.DiscordId, requester.Username),
            notifier);

        var result = await handler.Handle(
            new DeleteCraftingRequestCommand(request.Id),
            CancellationToken.None);

        Assert.True(result.Success);

        var updatedCraftingRequest = await db.CraftingRequests.SingleAsync();
        Assert.Equal(RequestStatus.Cancelled, updatedCraftingRequest.Status);

        var updatedLinkedOpen = await db.MaterialProcurementRequests.FindAsync(linkedOpen.Id);
        var updatedLinkedCompleted = await db.MaterialProcurementRequests.FindAsync(linkedCompleted.Id);
        var updatedUnrelated = await db.MaterialProcurementRequests.FindAsync(unrelated.Id);

        Assert.Equal(RequestStatus.Cancelled, updatedLinkedOpen!.Status);
        Assert.Null(updatedLinkedOpen.CompletedAt);
        Assert.Equal(RequestStatus.Cancelled, updatedLinkedCompleted!.Status);
        Assert.Null(updatedLinkedCompleted.CompletedAt);
        Assert.Equal(RequestStatus.Open, updatedUnrelated!.Status);

        Assert.Contains(("CraftingRequestUpdated", request.Id), notifier.Events);
        Assert.Contains(("ProcurementUpdated", linkedOpen.Id), notifier.Events);
        Assert.Contains(("ProcurementUpdated", linkedCompleted.Id), notifier.Events);
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

    private sealed class RecordingCraftingWorkflowNotifier : ICraftingWorkflowNotifier
    {
        public List<(string EventName, Guid RequestId)> Events { get; } = new();

        public Task NotifyAsync(string eventName, Guid requestId, CancellationToken cancellationToken)
        {
            Events.Add((eventName, requestId));
            return Task.CompletedTask;
        }

        public Task NotifyClaimedAsync(string requesterDiscordId, string claimerDiscordId, string claimerDisplayName, string requesterDisplayName, Guid requestId, string requestType, string requestLabel, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
