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

    // ─── DIAGNOSTIC PING ────────────────────────────────────────────────────────
    // Hit GET /api/v1/blueprints/ping to verify DB connectivity without auth.
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

    // ─── SEARCH ─────────────────────────────────────────────────────────────────
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

        // ── Step 1: Verify DB connectivity ──────────────────────────────────────
        bool canConnect;
        try
        {
            canConnect = await _db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB connectivity check threw an exception.");
            return StatusCode(500, new
            {
                message = "Database connectivity check failed.",
                detail = ex.Message,
                inner = ex.InnerException?.Message,
                type = ex.GetType().FullName
            });
        }

        if (!canConnect)
        {
            _logger.LogError("Database.CanConnectAsync returned false. ConnectionString preview={Preview}",
                PreviewConnectionString(_db.Database.GetConnectionString()));

            return StatusCode(500, new
            {
                message = "Cannot connect to the database. Check your connection string and ensure the server is running.",
                connectionStringPreview = PreviewConnectionString(_db.Database.GetConnectionString())
            });
        }

        // ── Step 2: Check for pending migrations ────────────────────────────────
        try
        {
            var pending = (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
            {
                _logger.LogWarning("There are {Count} pending migrations: {Migrations}", pending.Count, string.Join(", ", pending));
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — log and continue
            _logger.LogWarning(ex, "Could not check pending migrations (non-fatal).");
        }

        // ── Step 3: Check that the Blueprints table is queryable ────────────────
        try
        {
            var tableExists = await _db.Blueprints.AnyAsync(ct);
            _logger.LogInformation("Blueprints table reachable. HasRows={HasRows}", tableExists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprints table query threw. This usually means the table doesn't exist or migrations haven't been applied.");
            return StatusCode(500, new
            {
                message = "Blueprints table is not accessible. You may need to run 'dotnet ef database update'.",
                detail = ex.Message,
                inner = ex.InnerException?.Message,
                type = ex.GetType().FullName
            });
        }

        // ── Step 4: Run the actual search ───────────────────────────────────────
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

            _logger.LogInformation("Blueprint search loaded {Count} candidate blueprints before text filtering.", blueprints.Count);

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
                    VerifiedOwnerCount = 0
                })
                .Take(100)
                .ToList();

            _logger.LogInformation("Blueprint search returning {Count} results.", items.Count);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint search query failed. QueryQ={QueryQ} Category={Category} InGameOnly={InGameOnly}", q, category, inGameOnly);
            return StatusCode(500, new
            {
                message = "Blueprint search query failed.",
                detail = ex.Message,
                inner = ex.InnerException?.Message,
                type = ex.GetType().FullName
            });
        }
    }

    // ─── GET BY ID ──────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlueprintDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Blueprint detail endpoint hit. Path={Path} BlueprintId={BlueprintId} User={User}",
            HttpContext.Request.Path, id, User?.Identity?.Name ?? "<anonymous>");

        try
        {
            var blueprint = await _db.Blueprints
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

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
                    DataConfidence = "0.1"
                };

                _db.Blueprints.Add(placeholder);
                await _db.SaveChangesAsync(ct);
                blueprint = placeholder;
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

    // ─── HELPERS ────────────────────────────────────────────────────────────────
    private static string PreviewConnectionString(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return "<null or empty>";
        // Show only the first 60 chars to avoid leaking credentials in logs
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