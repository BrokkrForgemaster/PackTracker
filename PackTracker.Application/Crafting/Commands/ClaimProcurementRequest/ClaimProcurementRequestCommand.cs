using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.ClaimProcurementRequest;

public sealed record ClaimProcurementRequestCommand(Guid RequestId) : IRequest<OperationResult<Guid>>;

public sealed class ClaimProcurementRequestCommandHandler : IRequestHandler<ClaimProcurementRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public ClaimProcurementRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(ClaimProcurementRequestCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var request = await _db.MaterialProcurementRequests
            .FirstOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken);
        if (request is null)
            return OperationResult<Guid>.Fail("Procurement request not found.");

        if (request.Status != RequestStatus.Open)
            return OperationResult<Guid>.Fail("Only open requests can be claimed.");

        request.AssignedToProfileId = profile.Id;
        request.Status = RequestStatus.Accepted;
        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("ProcurementUpdated", command.RequestId, cancellationToken);

        return OperationResult<Guid>.Ok(command.RequestId);
    }
}
