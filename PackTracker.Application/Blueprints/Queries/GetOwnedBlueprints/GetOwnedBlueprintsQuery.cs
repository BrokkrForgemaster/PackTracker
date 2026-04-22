using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Blueprints.Queries.GetOwnedBlueprints;

public sealed record GetOwnedBlueprintsQuery : IRequest<IReadOnlyList<OwnedBlueprintSummaryDto>>;

public sealed class GetOwnedBlueprintsQueryHandler
    : IRequestHandler<GetOwnedBlueprintsQuery, IReadOnlyList<OwnedBlueprintSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetOwnedBlueprintsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<OwnedBlueprintSummaryDto>> Handle(
        GetOwnedBlueprintsQuery request,
        CancellationToken cancellationToken)
    {
        var currentProfileId = await _db.Profiles
            .AsNoTracking()
            .Where(x => x.DiscordId == _currentUser.UserId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!currentProfileId.HasValue)
            return Array.Empty<OwnedBlueprintSummaryDto>();

        var ownedBlueprints = await _db.MemberBlueprintOwnerships
            .AsNoTracking()
            .Where(x => x.MemberProfileId == currentProfileId.Value && x.InterestType == MemberBlueprintInterestType.Owns)
            .Include(x => x.Blueprint)
            .OrderBy(x => x.Blueprint!.BlueprintName)
            .Select(x => new
            {
                x.BlueprintId,
                x.AvailabilityStatus,
                OwnershipStatus = x.OwnershipStatus.ToString(),
                x.VerifiedAt,
                x.Notes,
                BlueprintName = x.Blueprint != null ? x.Blueprint.BlueprintName : "Unknown Blueprint",
                CraftedItemName = x.Blueprint != null ? x.Blueprint.CraftedItemName : string.Empty,
                Category = x.Blueprint != null ? x.Blueprint.Category : string.Empty,
                WikiUuid = x.Blueprint != null ? x.Blueprint.WikiUuid : null
            })
            .ToListAsync(cancellationToken);

        if (ownedBlueprints.Count == 0)
            return Array.Empty<OwnedBlueprintSummaryDto>();

        var blueprintIds = ownedBlueprints.Select(x => x.BlueprintId).Distinct().ToList();

        var materialLookup = await _db.BlueprintRecipes
            .AsNoTracking()
            .Where(x => blueprintIds.Contains(x.BlueprintId))
            .Join(
                _db.BlueprintRecipeMaterials.AsNoTracking().Include(x => x.Material),
                recipe => recipe.Id,
                material => material.BlueprintRecipeId,
                (recipe, material) => new
                {
                    recipe.BlueprintId,
                    Material = new BlueprintRecipeMaterialDto
                    {
                        MaterialId = material.MaterialId,
                        MaterialName = material.Material != null ? material.Material.Name : "Unknown Material",
                        MaterialType = material.Material != null ? material.Material.MaterialType : string.Empty,
                        Tier = material.Material != null ? material.Material.Tier : string.Empty,
                        QuantityRequired = material.QuantityRequired,
                        Unit = material.Unit,
                        IsOptional = material.IsOptional,
                        IsIntermediateCraftable = material.IsIntermediateCraftable,
                        SourceType = material.Material != null ? material.Material.SourceType.ToString() : "Unknown"
                    }
                })
            .ToListAsync(cancellationToken);

        var materialsByBlueprintId = materialLookup
            .GroupBy(x => x.BlueprintId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<BlueprintRecipeMaterialDto>)x
                    .Select(m => m.Material)
                    .OrderBy(m => m.MaterialName)
                    .ToList());

        return ownedBlueprints
            .Select(x => new OwnedBlueprintSummaryDto
            {
                BlueprintId = x.BlueprintId,
                WikiUuid = Guid.TryParse(x.WikiUuid, out var wikiUuid) ? wikiUuid : x.BlueprintId,
                BlueprintName = x.BlueprintName,
                CraftedItemName = x.CraftedItemName,
                Category = x.Category,
                AvailabilityStatus = x.AvailabilityStatus,
                OwnershipStatus = x.OwnershipStatus,
                VerifiedAt = x.VerifiedAt,
                Notes = x.Notes,
                Materials = materialsByBlueprintId.TryGetValue(x.BlueprintId, out var materials)
                    ? materials
                    : Array.Empty<BlueprintRecipeMaterialDto>()
            })
            .ToList();
    }
}
