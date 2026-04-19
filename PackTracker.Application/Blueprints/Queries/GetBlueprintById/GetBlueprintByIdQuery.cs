using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Blueprints.Queries.GetBlueprintById;

public sealed record GetBlueprintByIdQuery(Guid Id) : IRequest<BlueprintDetailDto?>;

public sealed class GetBlueprintByIdQueryHandler : IRequestHandler<GetBlueprintByIdQuery, BlueprintDetailDto?>
{
    private readonly IApplicationDbContext _db;
    private readonly IWikiSyncService _wikiSync;
    private readonly ILogger<GetBlueprintByIdQueryHandler> _logger;

    public GetBlueprintByIdQueryHandler(
        IApplicationDbContext db,
        IWikiSyncService wikiSync,
        ILogger<GetBlueprintByIdQueryHandler> logger)
    {
        _db = db;
        _wikiSync = wikiSync;
        _logger = logger;
    }

    public async Task<BlueprintDetailDto?> Handle(GetBlueprintByIdQuery request, CancellationToken cancellationToken)
    {
        var wikiUuidString = request.Id.ToString();

        var blueprint = await _db.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id || x.WikiUuid == wikiUuidString, cancellationToken);

        if (blueprint is null)
        {
            _logger.LogInformation("Blueprint {BlueprintId} not found locally, attempting on-demand wiki sync.", request.Id);
            var success = await _wikiSync.SyncBlueprintAsync(request.Id, cancellationToken);
            if (success)
            {
                blueprint = await _db.Blueprints
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.WikiUuid == wikiUuidString, cancellationToken);
            }
        }

        if (blueprint is null)
            return null;

        var recipe = await _db.BlueprintRecipes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BlueprintId == blueprint.Id, cancellationToken);

        var materials = recipe is null
            ? new List<BlueprintRecipeMaterialDto>()
            : await _db.BlueprintRecipeMaterials
                .AsNoTracking()
                .Where(x => x.BlueprintRecipeId == recipe.Id)
                .Include(x => x.Material)
                .OrderBy(x => x.Material!.Name)
                .Select(x => new BlueprintRecipeMaterialDto
                {
                    MaterialId = x.MaterialId,
                    MaterialName = x.Material != null ? x.Material.Name : "Unknown Material",
                    MaterialType = x.Material != null ? x.Material.MaterialType : string.Empty,
                    Tier = x.Material != null ? x.Material.Tier : string.Empty,
                    QuantityRequired = x.QuantityRequired,
                    Unit = x.Unit,
                    IsOptional = x.IsOptional,
                    IsIntermediateCraftable = x.IsIntermediateCraftable,
                    SourceType = x.Material != null ? x.Material.SourceType.ToString() : "Unknown"
                })
                .ToListAsync(cancellationToken);

        var owners = await _db.MemberBlueprintOwnerships
            .AsNoTracking()
            .Where(x => x.BlueprintId == blueprint.Id)
            .Include(x => x.MemberProfile)
            .OrderByDescending(x => x.OwnershipStatus)
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

        var components = await LoadComponentsAsync(blueprint.Slug, cancellationToken);

        return new BlueprintDetailDto
        {
            Id = blueprint.Id,
            WikiUuid = Guid.TryParse(blueprint.WikiUuid, out var wikiUuid) ? wikiUuid : request.Id,
            BlueprintName = blueprint.BlueprintName,
            CraftedItemName = blueprint.CraftedItemName,
            Category = blueprint.Category,
            Description = blueprint.Description,
            IsInGameAvailable = blueprint.IsInGameAvailable,
            AcquisitionLocation = blueprint.AcquisitionLocation,
            AcquisitionMethod = blueprint.AcquisitionMethod,
            SourceVersion = blueprint.SourceVersion,
            DataConfidence = blueprint.DataConfidence,
            Notes = blueprint.Notes,
            OutputQuantity = recipe?.OutputQuantity ?? 1,
            CraftingStationType = recipe?.CraftingStationType,
            TimeToCraftSeconds = recipe?.TimeToCraftSeconds,
            Materials = materials,
            Owners = owners,
            OwnerCount = owners.Count(x =>
                string.Equals(x.InterestType, MemberBlueprintInterestType.Owns.ToString(), StringComparison.OrdinalIgnoreCase)),
            Components = components
        };
    }

    private async Task<IReadOnlyList<BlueprintComponentDto>> LoadComponentsAsync(string? slug, CancellationToken cancellationToken)
    {
        try
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "scunpacked-data", "blueprints.json"));

            if (!File.Exists(sourcePath))
                return Array.Empty<BlueprintComponentDto>();

            await using var stream = File.OpenRead(sourcePath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            foreach (var record in document.RootElement.EnumerateArray())
            {
                var key = record.TryGetProperty("key", out var keyElement) ? keyElement.GetString() : null;
                var uuid = record.TryGetProperty("uuid", out var uuidElement) ? uuidElement.GetString() : null;

                if (!string.Equals(key, slug, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(uuid, slug, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!record.TryGetProperty("tiers", out var tiersElement)
                    || tiersElement.ValueKind != JsonValueKind.Array
                    || tiersElement.GetArrayLength() == 0)
                    return Array.Empty<BlueprintComponentDto>();

                var tier = tiersElement.EnumerateArray().FirstOrDefault();
                if (!tier.TryGetProperty("requirements", out var requirementsElement))
                    return Array.Empty<BlueprintComponentDto>();

                var components = new List<BlueprintComponentDto>();
                if (requirementsElement.TryGetProperty("children", out var rootChildren)
                    && rootChildren.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in rootChildren.EnumerateArray())
                    {
                        if (!child.TryGetProperty("kind", out var kindElement)
                            || !string.Equals(kindElement.GetString(), "group", StringComparison.OrdinalIgnoreCase))
                            continue;

                        components.Add(BuildComponent(child));
                    }
                }

                return components;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load source component data for blueprint {Slug}", slug);
        }

        return Array.Empty<BlueprintComponentDto>();
    }

    private static BlueprintComponentDto BuildComponent(JsonElement groupNode)
    {
        var partName = groupNode.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString() ?? "Component"
            : "Component";

        var materialName = "Unknown Material";
        double quantity = 0;

        if (groupNode.TryGetProperty("children", out var childrenElement)
            && childrenElement.ValueKind == JsonValueKind.Array)
        {
            var firstResource = childrenElement.EnumerateArray().FirstOrDefault(x =>
                x.TryGetProperty("kind", out var childKind)
                && string.Equals(childKind.GetString(), "resource", StringComparison.OrdinalIgnoreCase));

            if (firstResource.ValueKind != JsonValueKind.Undefined)
            {
                materialName = firstResource.TryGetProperty("name", out var materialNameElement)
                    ? materialNameElement.GetString() ?? materialName
                    : materialName;

                if (firstResource.TryGetProperty("quantity_scu", out var quantityScuElement)
                    && quantityScuElement.TryGetDouble(out var scuValue))
                    quantity = scuValue;
                else if (firstResource.TryGetProperty("quantity", out var quantityElement)
                         && quantityElement.TryGetDouble(out var quantityValue))
                    quantity = quantityValue;
            }
        }

        var modifiers = new List<BlueprintModifierDto>();
        if (groupNode.TryGetProperty("modifiers", out var modifiersElement)
            && modifiersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var modifierElement in modifiersElement.EnumerateArray())
            {
                var propertyKey = modifierElement.TryGetProperty("property_key", out var propertyKeyElement)
                    ? propertyKeyElement.GetString() ?? "modifier"
                    : "modifier";

                var atMin = 0d;
                var atMax = 0d;

                if (modifierElement.TryGetProperty("modifier_range", out var modifierRangeElement))
                {
                    if (modifierRangeElement.TryGetProperty("at_min_quality", out var minElement)
                        && minElement.TryGetDouble(out var minValue))
                        atMin = minValue;

                    if (modifierRangeElement.TryGetProperty("at_max_quality", out var maxElement)
                        && maxElement.TryGetDouble(out var maxValue))
                        atMax = maxValue;
                }

                modifiers.Add(new BlueprintModifierDto
                {
                    PropertyKey = propertyKey,
                    AtMinQuality = atMin,
                    AtMaxQuality = atMax
                });
            }
        }

        return new BlueprintComponentDto
        {
            PartName = partName,
            MaterialName = materialName,
            Quantity = quantity,
            DefaultQuality = 500,
            Modifiers = modifiers
        };
    }
}
