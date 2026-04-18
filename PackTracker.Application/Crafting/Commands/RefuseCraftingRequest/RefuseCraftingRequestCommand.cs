using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.RefuseCraftingRequest;

public sealed record RefuseCraftingRequestCommand(Guid RequestId, RefuseRequestDto Request) : IRequest<OperationResult<Guid>>;

public sealed class RefuseCraftingRequestCommandHandler : IRequestHandler<RefuseCraftingRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public RefuseCraftingRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(RefuseCraftingRequestCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var entity = await _db.CraftingRequests
            .FirstOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken);
        if (entity is null)
            return OperationResult<Guid>.Fail("Crafting request not found.");

        entity.Status = RequestStatus.Refused;
        entity.RefusalReason = command.Request.Reason.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("CraftingRequestUpdated", command.RequestId, cancellationToken);

        return OperationResult<Guid>.Ok(command.RequestId);
    }
}
