using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Queries.GetCraftingRequests;

public sealed record GetCraftingRequestsQuery : IRequest<IReadOnlyList<CraftingRequestListItemDto>>;

public sealed class GetCraftingRequestsQueryHandler : IRequestHandler<GetCraftingRequestsQuery, IReadOnlyList<CraftingRequestListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetCraftingRequestsQueryHandler> _logger;

    public GetCraftingRequestsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<GetCraftingRequestsQueryHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CraftingRequestListItemDto>> Handle(GetCraftingRequestsQuery request, CancellationToken cancellationToken)
    {
        var currentProfileId = await _db.Profiles
            .AsNoTracking()
            .Where(x => x.DiscordId == _currentUser.UserId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "[DIAGNOSTIC] Identity resolution: DiscordId={DiscordId}, ProfileId={ProfileId}",
            _currentUser.UserId,
            currentProfileId?.ToString() ?? "NULL");

        _logger.LogInformation(
            "[DIAGNOSTIC] Applying crafting filters: ProfileId={ProfileId}, StatusExclusions={Statuses}",
            currentProfileId?.ToString() ?? "NULL",
            "Cancelled, Completed");

        try
        {
            var rows = await BuildFullProjectionQuery(currentProfileId).ToListAsync(cancellationToken);
            _logger.LogInformation("[DIAGNOSTIC] GetCraftingRequests returned {Count} rows", rows.Count);
            return MapRows(rows);
        }
        catch (Exception ex) when (IsLegacyCraftingMetadataFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "Crafting request query failed with newer metadata columns; retrying with legacy-safe projection.");

            var rows = await BuildLegacyProjectionQuery(currentProfileId).ToListAsync(cancellationToken);
            return MapRows(rows);
        }
    }

    private IQueryable<CraftingRequestRow> BuildFullProjectionQuery(Guid? currentProfileId) =>
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
        where (req.Status != RequestStatus.Cancelled && req.Status != RequestStatus.Completed)
           || req.RequesterProfileId == currentProfileId
           || req.AssignedCrafterProfileId == currentProfileId
        where req.Status == RequestStatus.Open
            || req.RequesterProfileId == currentProfileId
            || req.AssignedCrafterProfileId == currentProfileId
        orderby req.CreatedAt descending
        select new CraftingRequestRow(
            req.Id,
            req.BlueprintId,
            req.ItemName,
            blueprint != null ? blueprint.CraftedItemName : null,
            blueprint != null ? blueprint.BlueprintName : null,
            requester != null ? requester.Username : null,
            requester != null ? requester.DiscordDisplayName : null,
            assignedCrafter != null ? assignedCrafter.Username : null,
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
            (Guid?)recipeMaterial.MaterialId,
            material != null ? material.Name : null,
            material != null ? material.MaterialType : null,
            material != null ? material.Tier : null,
            material != null ? material.SourceType.ToString() : null,
            (double?)recipeMaterial.QuantityRequired,
            recipeMaterial != null ? recipeMaterial.Unit : null,
            (bool?)recipeMaterial.IsOptional,
            (bool?)recipeMaterial.IsIntermediateCraftable);

    private IQueryable<CraftingRequestRow> BuildLegacyProjectionQuery(Guid? currentProfileId) =>
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
        where (req.Status != RequestStatus.Cancelled && req.Status != RequestStatus.Completed)
           || req.RequesterProfileId == currentProfileId
           || req.AssignedCrafterProfileId == currentProfileId
        where req.Status == RequestStatus.Open
            || req.RequesterProfileId == currentProfileId
            || req.AssignedCrafterProfileId == currentProfileId
        orderby req.CreatedAt descending
        select new CraftingRequestRow(
            req.Id,
            req.BlueprintId,
            null,
            blueprint != null ? blueprint.CraftedItemName : null,
            blueprint != null ? blueprint.BlueprintName : null,
            requester != null ? requester.Username : null,
            requester != null ? requester.DiscordDisplayName : null,
            assignedCrafter != null ? assignedCrafter.Username : null,
            req.QuantityRequested,
            req.MinimumQuality,
            req.RefusalReason,
            req.Priority,
            req.Status,
            MaterialSupplyMode.Negotiable,
            req.DeliveryLocation,
            req.RewardOffered,
            req.RequiredBy,
            req.Notes,
            req.CreatedAt,
            null,
            null,
            (Guid?)recipeMaterial.MaterialId,
            material != null ? material.Name : null,
            material != null ? material.MaterialType : null,
            material != null ? material.Tier : null,
            material != null ? material.SourceType.ToString() : null,
            (double?)recipeMaterial.QuantityRequired,
            recipeMaterial != null ? recipeMaterial.Unit : null,
            (bool?)recipeMaterial.IsOptional,
            (bool?)recipeMaterial.IsIntermediateCraftable);

    private static List<CraftingRequestListItemDto> MapRows(IReadOnlyList<CraftingRequestRow> rows)
    {
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

    private static bool IsLegacyCraftingMetadataFailure(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("ItemName", StringComparison.OrdinalIgnoreCase)
               || message.Contains("MaterialSupplyMode", StringComparison.OrdinalIgnoreCase)
               || message.Contains("RequesterTimeZoneDisplayName", StringComparison.OrdinalIgnoreCase)
               || message.Contains("RequesterUtcOffsetMinutes", StringComparison.OrdinalIgnoreCase)
               || message.Contains("column", StringComparison.OrdinalIgnoreCase) && message.Contains("CraftingRequests", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CraftingRequestRow(
        Guid RequestId,
        Guid BlueprintId,
        string? RequestItemName,
        string? BlueprintCraftedItemName,
        string? BlueprintName,
        string? RequesterUsername,
        string? RequesterDisplayName,
        string? AssignedCrafterUsername,
        int QuantityRequested,
        int MinimumQuality,
        string? RefusalReason,
        RequestPriority Priority,
        RequestStatus Status,
        MaterialSupplyMode MaterialSupplyMode,
        string? DeliveryLocation,
        string? RewardOffered,
        DateTime? RequiredBy,
        string? Notes,
        DateTime CreatedAt,
        string? RequesterTimeZoneDisplayName,
        int? RequesterUtcOffsetMinutes,
        Guid? MaterialId,
        string? MaterialName,
        string? MaterialType,
        string? MaterialTier,
        string? MaterialSourceType,
        double? QuantityRequired,
        string? Unit,
        bool? IsOptional,
        bool? IsIntermediateCraftable);
}
