using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.DeleteCraftingRequest;

public sealed record DeleteCraftingRequestCommand(Guid RequestId) : IRequest<OperationResult<Guid>>;

public sealed class DeleteCraftingRequestCommandHandler : IRequestHandler<DeleteCraftingRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public DeleteCraftingRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(DeleteCraftingRequestCommand command, CancellationToken cancellationToken)
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

        if (!_currentUser.CanManage(profile, request.RequesterProfileId))
            return OperationResult<Guid>.Fail("Only the creator or authorized leadership may remove this request.");

        if (request.Status == RequestStatus.Cancelled)
            return OperationResult<Guid>.Fail("Request is already cancelled.");

        var now = DateTime.UtcNow;
        request.Status = RequestStatus.Cancelled;
        request.UpdatedAt = now;

        var linkedProcurementRequests = await _db.MaterialProcurementRequests
            .Where(x => x.LinkedCraftingRequestId == command.RequestId
                     && x.Status != RequestStatus.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var linkedProcurementRequest in linkedProcurementRequests)
        {
            linkedProcurementRequest.Status = RequestStatus.Cancelled;
            linkedProcurementRequest.UpdatedAt = now;
            linkedProcurementRequest.CompletedAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _notifier.NotifyAsync("CraftingRequestUpdated", command.RequestId, cancellationToken);

        foreach (var linkedProcurementRequest in linkedProcurementRequests)
            await _notifier.NotifyAsync("ProcurementUpdated", linkedProcurementRequest.Id, cancellationToken);

        return OperationResult<Guid>.Ok(command.RequestId);
    }
}
