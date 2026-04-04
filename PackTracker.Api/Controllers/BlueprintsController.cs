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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BlueprintSearchItemDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] bool inGameOnly = true,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Blueprint search endpoint hit. Path={Path} QueryQ={QueryQ} Category={Category} InGameOnly={InGameOnly} User={User}",
            HttpContext.Request.Path,
            q,
            category,
            inGameOnly,
            User?.Identity?.Name ?? "<anonymous>");

        var query = _db.Blueprints.AsNoTracking().AsQueryable();

        if (inGameOnly)
            query = query.Where(x => x.IsInGameAvailable);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.BlueprintName, $"%{term}%") ||
                EF.Functions.ILike(x.CraftedItemName, $"%{term}%") ||
                EF.Functions.ILike(x.Category, $"%{term}%"));
        }

        var blueprints = await query
            .OrderBy(x => x.BlueprintName)
            .Take(100)
            .ToListAsync(ct);

        var blueprintIds = blueprints.Select(x => x.Id).ToList();
        var verifiedOwnerCounts = await _db.MemberBlueprintOwnerships
            .AsNoTracking()
            .Where(o => blueprintIds.Contains(o.BlueprintId)
                        && o.InterestType == MemberBlueprintInterestType.Owns
                        && o.OwnershipStatus == BlueprintOwnershipStatus.Verified)
            .GroupBy(o => o.BlueprintId)
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
                VerifiedOwnerCount = verifiedOwnerCounts.TryGetValue(x.Id, out var count) ? count : 0
            })
            .ToList();

        _logger.LogInformation("Blueprint search returned {Count} results", items.Count);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlueprintDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Blueprint detail endpoint hit. Path={Path} BlueprintId={BlueprintId} User={User}",
            HttpContext.Request.Path,
            id,
            User?.Identity?.Name ?? "<anonymous>");

        // 1. Try to find the blueprint in our local database
        var blueprint = await _db.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        // 2. If it's missing from our DB (new Wiki item), create a placeholder 
        // so the Ownership and Crafting Request systems have a foreign key to attach to.
        if (blueprint is null)
        {
            _logger.LogWarning("Blueprint {Id} not found in database. Creating placeholder record.", id);

            var placeholder = new Blueprint
            {
                Id = id,
                BlueprintName = "New Discovery (Syncing...)",
                CraftedItemName = "Unknown Item",
                Category = "Unknown",
                IsInGameAvailable = true,
                DataConfidence = "0.1" // Low confidence until a background sync hits it
            };

            _db.Blueprints.Add(placeholder);
            await _db.SaveChangesAsync(ct);
            blueprint = placeholder;
        }

        // 3. Fetch local Recipe/Materials if they exist
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

        // 4. Fetch House Wolf Owners (This is why we need the local DB)
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

        // 5. Build and return the DTO
        var dto = new BlueprintDetailDto
        {
            Id = blueprint.Id,
            BlueprintName = blueprint.BlueprintName,
            CraftedItemName = blueprint.CraftedItemName,
            Category = blueprint.Category,
            Description = blueprint.Description,
            IsInGameAvailable = blueprint.IsInGameAvailable,
            AcquisitionSummary = blueprint.AcquisitionSummary,
            AcquisitionLocation = blueprint.AcquisitionLocation,
            AcquisitionMethod = blueprint.AcquisitionMethod,
            SourceVersion = blueprint.SourceVersion,
            DataConfidence = blueprint.DataConfidence,
            Notes = blueprint.Notes,
            OutputQuantity = recipe?.OutputQuantity ?? 1,
            CraftingStationType = recipe?.CraftingStationType,
            TimeToCraftSeconds = recipe?.TimeToCraftSeconds,
            Materials = materials,
            Owners = owners
        };

        return Ok(dto);
    }
}
