using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize]
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

        var items = await query
            .OrderBy(x => x.BlueprintName)
            .Select(x => new BlueprintSearchItemDto
            {
                Id = x.Id,
                BlueprintName = x.BlueprintName,
                CraftedItemName = x.CraftedItemName,
                Category = x.Category,
                IsInGameAvailable = x.IsInGameAvailable,
                AcquisitionSummary = x.AcquisitionSummary,
                DataConfidence = x.DataConfidence,
                VerifiedOwnerCount = _db.MemberBlueprintOwnerships.Count(o =>
                    o.BlueprintId == x.Id && o.OwnershipStatus == BlueprintOwnershipStatus.Verified)
            })
            .Take(100)
            .ToListAsync(ct);

        _logger.LogInformation("Blueprint search returned {Count} results", items.Count);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlueprintDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var blueprint = await _db.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (blueprint is null)
            return NotFound();

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
                    MaterialName = x.Material != null ? x.Material.Name : string.Empty,
                    MaterialType = x.Material != null ? x.Material.MaterialType : string.Empty,
                    Tier = x.Material != null ? x.Material.Tier : string.Empty,
                    QuantityRequired = x.QuantityRequired,
                    Unit = x.Unit,
                    IsOptional = x.IsOptional,
                    IsIntermediateCraftable = x.IsIntermediateCraftable,
                    SourceType = x.Material != null ? x.Material.SourceType.ToString() : MaterialSourceType.Unknown.ToString()
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
                Username = x.MemberProfile != null ? x.MemberProfile.Username : string.Empty,
                OwnershipStatus = x.OwnershipStatus.ToString(),
                AvailabilityStatus = x.AvailabilityStatus,
                VerifiedAt = x.VerifiedAt,
                Notes = x.Notes
            })
            .ToListAsync(ct);

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
