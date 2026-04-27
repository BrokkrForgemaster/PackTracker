using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
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
            .Include(r => r.Material)
            .FirstOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken);
        if (request is null)
            return OperationResult<Guid>.Fail("Procurement request not found.");

        if (request.Status != RequestStatus.Open)
            return OperationResult<Guid>.Fail("Only open requests can be claimed.");

        var currentClaims = await _db.RequestClaims
            .CountAsync(c => c.RequestId == command.RequestId && c.RequestType == "Procurement", cancellationToken);

        if (request.MaxClaims > 0 && currentClaims >= request.MaxClaims)
        {
            return OperationResult<Guid>.Fail("This request has already reached its maximum number of claims.");
        }

        var alreadyClaimed = await _db.RequestClaims
            .AnyAsync(c => c.RequestId == command.RequestId && c.RequestType == "Procurement" && c.ProfileId == profile.Id, cancellationToken);

        if (alreadyClaimed)
        {
            return OperationResult<Guid>.Fail("You have already claimed this request.");
        }

        var claim = new RequestClaim
        {
            RequestId = command.RequestId,
            RequestType = "Procurement",
            ProfileId = profile.Id,
            ClaimedAt = DateTime.UtcNow
        };

        _db.RequestClaims.Add(claim);

        // If this is the first claim, or it's a single-claim request, set the legacy assigned field
        if (request.AssignedToProfileId == null)
        {
            request.AssignedToProfileId = profile.Id;
        }

        // Only transition to Accepted if we've reached the MaxClaims
        if (request.MaxClaims > 0 && currentClaims + 1 >= request.MaxClaims)
        {
            request.Status = RequestStatus.Accepted;
        }

        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var requesterProfile = request.RequesterProfileId.HasValue
            ? await _db.Profiles.FirstOrDefaultAsync(x => x.Id == request.RequesterProfileId.Value, cancellationToken)
            : null;

        await _notifier.NotifyAsync("ProcurementUpdated", command.RequestId, cancellationToken);
        await _notifier.NotifyClaimedAsync(
            requesterDiscordId: requesterProfile?.DiscordId ?? string.Empty,
            claimerDiscordId: profile.DiscordId,
            claimerDisplayName: profile.DiscordDisplayName ?? profile.Username,
            requesterDisplayName: requesterProfile?.DiscordDisplayName ?? requesterProfile?.Username ?? string.Empty,
            requestId: command.RequestId,
            requestType: "Procurement",
            requestLabel: request.Material?.Name ?? "Procurement Request",
            cancellationToken: cancellationToken);

        return OperationResult<Guid>.Ok(command.RequestId);
    }
}
