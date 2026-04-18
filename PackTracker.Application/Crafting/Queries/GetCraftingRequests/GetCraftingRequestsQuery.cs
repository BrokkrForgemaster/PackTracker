using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Queries.GetCraftingRequests;

public sealed record GetCraftingRequestsQuery : IRequest<IReadOnlyList<CraftingRequestListItemDto>>;

public sealed class GetCraftingRequestsQueryHandler : IRequestHandler<GetCraftingRequestsQuery, IReadOnlyList<CraftingRequestListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCraftingRequestsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CraftingRequestListItemDto>> Handle(GetCraftingRequestsQuery request, CancellationToken cancellationToken)
    {
        var currentUsername = _currentUser.DisplayName;

        var rows = await (
            from req in _db.CraftingRequests.AsNoTracking()
            join blueprint in _db.Blueprints.AsNoTracking() on req.BlueprintId equals blueprint.Id into blueprintGroup
            from blueprint in blueprintGroup.DefaultIfEmpty()
            join requester in _db.Profiles.AsNoTracking() on req.RequesterProfileId equals requester.Id into requesterGroup
            from requester in requesterGroup.DefaultIfEmpty()
            join assignedCrafter in _db.Profiles.AsNoTracking() on req.AssignedCrafterProfileId equals assignedCrafter.Id into assignedCrafterGroup
            from assignedCrafter in assignedCrafterGroup.DefaultIfEmpty()
            join recipe in _db.BlueprintRecipes.AsNoTracking() on req.BlueprintId equals recipe.BlueprintId into recipeGroup
            from recipe in recipeGroup.DefaultIfEmpty()
            join recipeMaterial in _db.BlueprintRecipeMaterials.AsNoTracking() on recipe.Id equals recipeMaterial.BlueprintRecipeId into recipeMaterialGroup
            from recipeMaterial in recipeMaterialGroup.DefaultIfEmpty()
            join material in _db.Materials.AsNoTracking() on recipeMaterial.MaterialId equals material.Id into materialGroup
            from material in materialGroup.DefaultIfEmpty()
            where req.Status != RequestStatus.Cancelled && req.Status != RequestStatus.Completed
            where req.Status == RequestStatus.Open
                || requester.Username == currentUsername
                || assignedCrafter.Username == currentUsername
            orderby req.CreatedAt descending
            select new
            {
                RequestId = req.Id,
                req.BlueprintId,
                RequestItemName = req.ItemName,
                BlueprintCraftedItemName = blueprint != null ? blueprint.CraftedItemName : null,
                BlueprintName = blueprint != null ? blueprint.BlueprintName : null,
                RequesterUsername = requester != null ? requester.Username : null,
                RequesterDisplayName = requester != null ? requester.DiscordDisplayName : null,
                AssignedCrafterUsername = assignedCrafter != null ? assignedCrafter.Username : null,
                req.QuantityRequested,
                req.MinimumQuality,
                req.RefusalReason,
                req.Priority,
                req.Status,
                req.MaterialSupplyMode,
                req.DeliveryLocation,
                req.RewardOffered,
                req.RequiredBy,
                req.Notes,
                req.CreatedAt,
                req.RequesterTimeZoneDisplayName,
                req.RequesterUtcOffsetMinutes,
                MaterialId = (Guid?)recipeMaterial.MaterialId,
                MaterialName = material != null ? material.Name : null,
                MaterialType = material != null ? material.MaterialType : null,
                MaterialTier = material != null ? material.Tier : null,
                MaterialSourceType = material != null ? material.SourceType.ToString() : null,
                QuantityRequired = (double?)recipeMaterial.QuantityRequired,
                Unit = recipeMaterial != null ? recipeMaterial.Unit : null,
                IsOptional = (bool?)recipeMaterial.IsOptional,
                IsIntermediateCraftable = (bool?)recipeMaterial.IsIntermediateCraftable
            }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new
            {
                x.RequestId,
                x.BlueprintId,
                x.RequestItemName,
                x.BlueprintCraftedItemName,
                x.BlueprintName,
                x.RequesterUsername,
                x.RequesterDisplayName,
                x.AssignedCrafterUsername,
                x.QuantityRequested,
                x.MinimumQuality,
                x.RefusalReason,
                x.Priority,
                x.Status,
                x.MaterialSupplyMode,
                x.DeliveryLocation,
                x.RewardOffered,
                x.RequiredBy,
                x.Notes,
                x.CreatedAt,
                x.RequesterTimeZoneDisplayName,
                x.RequesterUtcOffsetMinutes
            })
            .Select(group => new CraftingRequestListItemDto
            {
                Id = group.Key.RequestId,
                BlueprintId = group.Key.BlueprintId,
                BlueprintName = !string.IsNullOrWhiteSpace(group.Key.RequestItemName)
                    ? group.Key.RequestItemName
                    : (!string.IsNullOrWhiteSpace(group.Key.BlueprintCraftedItemName) && group.Key.BlueprintCraftedItemName != "Unknown"
                        ? group.Key.BlueprintCraftedItemName
                        : group.Key.BlueprintName?.Replace(" Blueprint", "", StringComparison.Ordinal)) ?? "Unknown",
                CraftedItemName = group.Key.BlueprintCraftedItemName ?? "Unknown Item",
                RequesterUsername = group.Key.RequesterUsername ?? "Unknown",
                RequesterDisplayName = group.Key.RequesterDisplayName ?? group.Key.RequesterUsername ?? "Unknown",
                AssignedCrafterUsername = group.Key.AssignedCrafterUsername,
                QuantityRequested = group.Key.QuantityRequested,
                MinimumQuality = group.Key.MinimumQuality,
                RefusalReason = group.Key.RefusalReason,
                Priority = group.Key.Priority.ToString(),
                Status = group.Key.Status.ToString(),
                MaterialSupplyMode = group.Key.MaterialSupplyMode.ToString(),
                DeliveryLocation = group.Key.DeliveryLocation,
                RewardOffered = group.Key.RewardOffered,
                RequiredBy = group.Key.RequiredBy,
                Notes = group.Key.Notes,
                CreatedAt = group.Key.CreatedAt,
                RequesterTimeZoneDisplayName = group.Key.RequesterTimeZoneDisplayName,
                RequesterUtcOffsetMinutes = group.Key.RequesterUtcOffsetMinutes,
                Materials = group.Where(x => x.MaterialId.HasValue)
                    .Select(x => new BlueprintRecipeMaterialDto
                    {
                        MaterialId = x.MaterialId!.Value,
                        MaterialName = x.MaterialName ?? "Unknown",
                        MaterialType = x.MaterialType ?? string.Empty,
                        Tier = x.MaterialTier ?? string.Empty,
                        QuantityRequired = x.QuantityRequired ?? 0,
                        Unit = x.Unit ?? string.Empty,
                        SourceType = x.MaterialSourceType ?? "Unknown",
                        IsOptional = x.IsOptional ?? false,
                        IsIntermediateCraftable = x.IsIntermediateCraftable ?? false
                    })
                    .ToList()
            })
            .ToList();
    }
}
