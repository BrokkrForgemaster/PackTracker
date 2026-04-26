using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;
using StatusUpdateResult = PackTracker.Application.Common.StatusUpdateResult;

namespace PackTracker.Application.Crafting.Commands.UpdateCraftingRequestStatus;

public sealed record UpdateCraftingRequestStatusCommand(Guid RequestId, string Status) : IRequest<StatusUpdateResult>;

public sealed class UpdateCraftingRequestStatusCommandHandler : IRequestHandler<UpdateCraftingRequestStatusCommand, StatusUpdateResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public UpdateCraftingRequestStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<StatusUpdateResult> Handle(UpdateCraftingRequestStatusCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RequestStatus>(command.Status, true, out var parsedStatus))
            return new StatusUpdateResult(false, $"Invalid status '{command.Status}'.");

        var request = await _db.CraftingRequests
            .FirstOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken);
        if (request is null)
            return new StatusUpdateResult(false, "Crafting request not found.");

        if (parsedStatus == RequestStatus.Cancelled)
        {
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                return new StatusUpdateResult(false, "Unauthorized");

            var profile = await _db.Profiles
                .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
            if (profile is null)
                return new StatusUpdateResult(false, "Unauthorized");

            if (!_currentUser.CanManage(profile, request.RequesterProfileId))
                return new StatusUpdateResult(false, "Only the creator or authorized leadership may cancel this request.");
        }
        else if (parsedStatus == RequestStatus.Completed)
        {
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                return new StatusUpdateResult(false, "Unauthorized");

            var profile = await _db.Profiles
                .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
            if (profile is null)
                return new StatusUpdateResult(false, "Unauthorized");

            if (profile.Id != request.RequesterProfileId)
                return new StatusUpdateResult(false, "Only the creator may complete this request.");
        }

        var previous = request.Status;
        var now = DateTime.UtcNow;
        request.Status = parsedStatus;
        request.UpdatedAt = now;
        if (parsedStatus == RequestStatus.Completed)
        {
            request.CompletedAt = now;

            var linkedProcurementRequests = await _db.MaterialProcurementRequests
                .Where(x => x.LinkedCraftingRequestId == request.Id
                         && x.Status != RequestStatus.Completed
                         && x.Status != RequestStatus.Cancelled)
                .ToListAsync(cancellationToken);

            foreach (var linkedProcurementRequest in linkedProcurementRequests)
            {
                linkedProcurementRequest.Status = RequestStatus.Completed;
                linkedProcurementRequest.UpdatedAt = now;
                linkedProcurementRequest.CompletedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("CraftingRequestUpdated", command.RequestId, cancellationToken);

        if (parsedStatus == RequestStatus.Completed)
        {
            var linkedProcurementIds = await _db.MaterialProcurementRequests
                .Where(x => x.LinkedCraftingRequestId == request.Id)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var linkedProcurementId in linkedProcurementIds)
                await _notifier.NotifyAsync("ProcurementUpdated", linkedProcurementId, cancellationToken);
        }

        return new StatusUpdateResult(true, "Status updated.", command.RequestId, previous.ToString(), parsedStatus.ToString());
    }
}
