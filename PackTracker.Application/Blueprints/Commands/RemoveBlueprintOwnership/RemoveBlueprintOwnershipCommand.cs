using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Blueprints.Commands.RemoveBlueprintOwnership;

public sealed record RemoveBlueprintOwnershipCommand(
    Guid BlueprintId) : IRequest<RemoveBlueprintOwnershipResult>;

public sealed record RemoveBlueprintOwnershipResult(
    bool Success,
    string Message,
    int? OwnerCount = null);

public sealed class RemoveBlueprintOwnershipCommandHandler
    : IRequestHandler<RemoveBlueprintOwnershipCommand, RemoveBlueprintOwnershipResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RemoveBlueprintOwnershipCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<RemoveBlueprintOwnershipResult> Handle(
        RemoveBlueprintOwnershipCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return new RemoveBlueprintOwnershipResult(false, "Unauthorized");
        }

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);

        if (profile is null)
        {
            return new RemoveBlueprintOwnershipResult(false, "Profile not found.");
        }

        var ownership = await _db.MemberBlueprintOwnerships
            .FirstOrDefaultAsync(x => x.BlueprintId == command.BlueprintId && x.MemberProfileId == profile.Id, cancellationToken);

        if (ownership is null)
        {
            return new RemoveBlueprintOwnershipResult(false, "Blueprint ownership not found.");
        }

        _db.MemberBlueprintOwnerships.Remove(ownership);

        var blueprint = await _db.Blueprints
            .FirstOrDefaultAsync(x => x.Id == command.BlueprintId, cancellationToken);

        if (blueprint != null)
        {
            await _db.SaveChangesAsync(cancellationToken);

            var ownerCount = await _db.MemberBlueprintOwnerships
                .AsNoTracking()
                .Where(x => x.BlueprintId == blueprint.Id && x.InterestType == MemberBlueprintInterestType.Owns)
                .CountAsync(cancellationToken);

            blueprint.OwnerCount = ownerCount;
            blueprint.UpdatedAt = DateTime.UtcNow;
            
            await _db.SaveChangesAsync(cancellationToken);
            
            return new RemoveBlueprintOwnershipResult(true, "Blueprint ownership removed.", ownerCount);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new RemoveBlueprintOwnershipResult(true, "Blueprint ownership removed.");
    }
}
