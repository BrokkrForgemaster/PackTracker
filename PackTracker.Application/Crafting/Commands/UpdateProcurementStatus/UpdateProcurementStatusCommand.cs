using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.UpdateProcurementStatus;

public sealed record UpdateProcurementStatusCommand(Guid RequestId, string Status) : IRequest<StatusUpdateResult>;

public sealed class UpdateProcurementStatusCommandHandler : IRequestHandler<UpdateProcurementStatusCommand, StatusUpdateResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public UpdateProcurementStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<StatusUpdateResult> Handle(UpdateProcurementStatusCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RequestStatus>(command.Status, true, out var parsedStatus))
            return new StatusUpdateResult(false, $"Invalid status '{command.Status}'.");

        var entity = await _db.MaterialProcurementRequests
            .FirstOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken);
        if (entity is null)
            return new StatusUpdateResult(false, "Procurement request not found.");

        if (parsedStatus == RequestStatus.Cancelled)
        {
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
                return new StatusUpdateResult(false, "Unauthorized");

            var profile = await _db.Profiles
                .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
            if (profile is null)
                return new StatusUpdateResult(false, "Unauthorized");

            if (!_currentUser.CanManage(profile, entity.RequesterProfileId))
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

            if (profile.Id != entity.RequesterProfileId)
                return new StatusUpdateResult(false, "Only the creator may complete this request.");
        }

        var previousStatus = entity.Status;
        entity.Status = parsedStatus;
        entity.UpdatedAt = DateTime.UtcNow;
        if (parsedStatus == RequestStatus.Completed)
            entity.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("ProcurementUpdated", entity.Id, cancellationToken);

        return new StatusUpdateResult(true, "Procurement request status updated.", entity.Id, previousStatus.ToString(), entity.Status.ToString());
    }
}
