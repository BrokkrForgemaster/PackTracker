using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.DeleteProcurementRequest;

public sealed record DeleteProcurementRequestCommand(Guid RequestId) : IRequest<OperationResult<Guid>>;

public sealed class DeleteProcurementRequestCommandHandler : IRequestHandler<DeleteProcurementRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public DeleteProcurementRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(DeleteProcurementRequestCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var entity = await _db.MaterialProcurementRequests
            .FirstOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken);
        if (entity is null)
            return OperationResult<Guid>.Fail("Procurement request not found.");

        if (!_currentUser.CanManage(profile, entity.RequesterProfileId))
            return OperationResult<Guid>.Fail("Only the creator or authorized leadership may remove this request.");

        if (entity.Status == RequestStatus.Cancelled)
            return OperationResult<Guid>.Fail("Request is already cancelled.");

        entity.Status = RequestStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("ProcurementUpdated", command.RequestId, cancellationToken);

        return OperationResult<Guid>.Ok(command.RequestId);
    }
}
