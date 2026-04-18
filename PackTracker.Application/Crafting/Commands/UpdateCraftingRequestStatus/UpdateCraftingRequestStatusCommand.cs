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

        var previous = request.Status;
        request.Status = parsedStatus;
        request.UpdatedAt = DateTime.UtcNow;
        if (parsedStatus == RequestStatus.Completed)
            request.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("CraftingRequestUpdated", command.RequestId, cancellationToken);

        return new StatusUpdateResult(true, "Status updated.", command.RequestId, previous.ToString(), parsedStatus.ToString());
    }
}
