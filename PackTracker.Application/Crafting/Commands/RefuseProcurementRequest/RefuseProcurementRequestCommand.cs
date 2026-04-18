using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.RefuseProcurementRequest;

public sealed record RefuseProcurementRequestCommand(Guid RequestId, RefuseRequestDto Request) : IRequest<OperationResult<Guid>>;

public sealed class RefuseProcurementRequestCommandHandler : IRequestHandler<RefuseProcurementRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public RefuseProcurementRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(RefuseProcurementRequestCommand command, CancellationToken cancellationToken)
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

        entity.Status = RequestStatus.Refused;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("ProcurementUpdated", command.RequestId, cancellationToken);

        return OperationResult<Guid>.Ok(command.RequestId);
    }
}
