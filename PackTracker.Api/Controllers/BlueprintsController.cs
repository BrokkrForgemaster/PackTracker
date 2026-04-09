using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/[controller]")]
public class BlueprintsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<BlueprintsController> _logger;

    public BlueprintsController(AppDbContext db, ILogger<BlueprintsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("ping")]
    [AllowAnonymous]
    public async Task<IActionResult> Ping(CancellationToken ct)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);
            var pendingMigrations = (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();
            var appliedMigrations = (await _db.Database.GetAppliedMigrationsAsync(ct)).ToList();

            return Ok(new
            {
                status = "ok",
                canConnect,
                provider = _db.Database.ProviderName,
                pendingMigrations,
                appliedMigrationsCount = appliedMigrations.Count,
                timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "db_error",
                message = ex.Message,
                inner = ex.InnerException?.Message,
                type = ex.GetType().FullName
            });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BlueprintSearchItemDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] bool inGameOnly = true,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Blueprint search endpoint hit. Path={Path} QueryQ={QueryQ} Category={Category} InGameOnly={InGameOnly} User={User}",
            HttpContext.Request.Path, q, category, inGameOnly,
            User?.Identity?.Name ?? "<anonymous>");

        try
        {
            IQueryable<Blueprint> query = _db.Blueprints.AsNoTracking();

            if (inGameOnly)
                query = query.Where(x => x.IsInGameAvailable);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(x => x.Category == category);

            var blueprints = await query
                .OrderBy(x => x.BlueprintName)
                .Take(200)
                .ToListAsync(ct);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                blueprints = blueprints
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.BlueprintName) && x.BlueprintName.Contains(term, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(x.CraftedItemName) && x.CraftedItemName.Contains(term, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(x.Category) && x.Category.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var blueprintIds = blueprints.Select(x => x.Id).ToList();

            var ownerCounts = await _db.MemberBlueprintOwnerships
                .AsNoTracking()
                .Where(x => blueprintIds.Contains(x.BlueprintId) && x.InterestType == MemberBlueprintInterestType.Owns)
                .GroupBy(x => x.BlueprintId)
                .Select(g => new { BlueprintId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BlueprintId, x => x.Count, ct);

            var items = blueprints
                .Select(x => new BlueprintSearchItemDto
                {
                    Id = x.Id,
                    BlueprintName = x.BlueprintName,
                    CraftedItemName = x.CraftedItemName,
                    Category = x.Category,
                    IsInGameAvailable = x.IsInGameAvailable,
                    AcquisitionSummary = x.AcquisitionSummary,
                    DataConfidence = x.DataConfidence,
                    VerifiedOwnerCount = ownerCounts.TryGetValue(x.Id, out var count) ? count : 0
                })
                .Take(100)
                .ToList();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint search query failed.");
            return StatusCode(500, new
            {
                message = "Blueprint search query failed.",
                detail = ex.Message,
                inner = ex.InnerException?.Message,
                type = ex.GetType().FullName
            });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlueprintDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        _logger.LogInformation(
            "Blueprint detail endpoint hit. Path={Path} BlueprintId={BlueprintId} User={User}",
            HttpContext.Request.Path,
            id,
            User?.Identity?.Name ?? "<anonymous>");

        try
        {
            var blueprint = await _db.Blueprints
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (blueprint is null)
            {
                _logger.LogWarning("Blueprint {Id} not found in database.", id);
                return NotFound(new { message = "Blueprint not found.", id });
            }

            var recipe = await _db.BlueprintRecipes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BlueprintId == id, ct);

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
                    .ToListAsync(ct);

            var owners = await _db.MemberBlueprintOwnerships
                .AsNoTracking()
                .Where(x => x.BlueprintId == id)
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
                .ToListAsync(ct);

            var components = await LoadComponentsFromSourceAsync(blueprint, ct);

            var dto = new BlueprintDetailDto
            {
                Id = blueprint.Id,
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
                    string.Equals(
                        x.InterestType,
                        MemberBlueprintInterestType.Owns.ToString(),
                        StringComparison.OrdinalIgnoreCase)),
                Components = components
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint detail failed for {BlueprintId}", id);
            return StatusCode(500, new
            {
                message = "Blueprint detail query failed.",
                detail = ex.Message,
                inner = ex.InnerException?.Message,
                type = ex.GetType().FullName
            });
        }
    }

    private async Task<IReadOnlyList<BlueprintComponentDto>> LoadComponentsFromSourceAsync(Blueprint blueprint, CancellationToken ct)
    {
        try
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "scunpacked-data", "blueprints.json"));

            if (!System.IO.File.Exists(sourcePath))
                return Array.Empty<BlueprintComponentDto>();

            await using var stream = System.IO.File.OpenRead(sourcePath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            foreach (var record in document.RootElement.EnumerateArray())
            {
                var key = record.TryGetProperty("key", out var keyElement) ? keyElement.GetString() : null;
                var uuid = record.TryGetProperty("uuid", out var uuidElement) ? uuidElement.GetString() : null;

                if (!string.Equals(key, blueprint.Slug, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(uuid, blueprint.Slug, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!record.TryGetProperty("tiers", out var tiersElement)
                    || tiersElement.ValueKind != JsonValueKind.Array
                    || tiersElement.GetArrayLength() == 0)
                {
                    return Array.Empty<BlueprintComponentDto>();
                }

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
                        {
                            continue;
                        }

                        components.Add(BuildComponent(child));
                    }
                }

                return components;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load source component data for blueprint {BlueprintSlug}", blueprint.Slug);
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
                {
                    quantity = scuValue;
                }
                else if (firstResource.TryGetProperty("quantity", out var quantityElement)
                         && quantityElement.TryGetDouble(out var quantityValue))
                {
                    quantity = quantityValue;
                }
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
                    {
                        atMin = minValue;
                    }

                    if (modifierRangeElement.TryGetProperty("at_max_quality", out var maxElement)
                        && maxElement.TryGetDouble(out var maxValue))
                    {
                        atMax = maxValue;
                    }
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

    private static string PreviewConnectionString(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs))
            return "<null or empty>";

        return cs.Length > 60 ? cs[..60] + "..." : cs;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken ct)
    {
        try
        {
            var categories = await _db.Blueprints
                .AsNoTracking()
                .Where(x => !string.IsNullOrEmpty(x.Category))
                .Select(x => x.Category!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(ct);

            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blueprint categories.");
            return StatusCode(500, new { message = "Failed to load categories.", detail = ex.Message });
        }
    }
}