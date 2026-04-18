using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Blueprints.Queries.GetBlueprintOwners;

public sealed record GetBlueprintOwnersQuery(Guid BlueprintId) : IRequest<IReadOnlyList<BlueprintOwnerDto>?>;

public sealed class GetBlueprintOwnersQueryHandler : IRequestHandler<GetBlueprintOwnersQuery, IReadOnlyList<BlueprintOwnerDto>?>
{
    private readonly IApplicationDbContext _db;

    public GetBlueprintOwnersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<BlueprintOwnerDto>?> Handle(GetBlueprintOwnersQuery request, CancellationToken cancellationToken)
    {
        var wikiUuidString = request.BlueprintId.ToString();
        var blueprint = await _db.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.BlueprintId || x.WikiUuid == wikiUuidString, cancellationToken);

        if (blueprint is null)
            return null;

        return await _db.MemberBlueprintOwnerships
            .AsNoTracking()
            .Where(x => x.BlueprintId == blueprint.Id)
            .Include(x => x.MemberProfile)
            .OrderByDescending(x => x.InterestType)
            .ThenBy(x => x.MemberProfile!.Username)
            .Select(x => new BlueprintOwnerDto
            {
                MemberProfileId = x.MemberProfileId,
                Username = x.MemberProfile != null ? x.MemberProfile.Username : "Unknown Member",
                InterestType = x.InterestType.ToString(),
                OwnershipStatus = x.OwnershipStatus.ToString(),
                AvailabilityStatus = x.AvailabilityStatus,
                VerifiedAt = x.VerifiedAt,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);
    }
}
