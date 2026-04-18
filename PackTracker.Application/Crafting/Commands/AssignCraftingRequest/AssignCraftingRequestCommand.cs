using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.AssignCraftingRequest;

public sealed record AssignCraftingRequestCommand(Guid RequestId) : IRequest<OperationResult<Guid>>;

public sealed class AssignCraftingRequestCommandHandler : IRequestHandler<AssignCraftingRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public AssignCraftingRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(AssignCraftingRequestCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var request = await _db.CraftingRequests
            .FirstOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken);
        if (request is null)
            return OperationResult<Guid>.Fail("Crafting request not found.");

        if (request.Status != RequestStatus.Open)
            return OperationResult<Guid>.Fail("Only open requests can be assigned.");

        request.AssignedCrafterProfileId = profile.Id;
        request.Status = RequestStatus.Accepted;
        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("CraftingRequestUpdated", command.RequestId, cancellationToken);

        return OperationResult<Guid>.Ok(command.RequestId);
    }
}
