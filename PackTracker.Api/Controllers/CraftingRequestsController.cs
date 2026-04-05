using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/crafting")]
public class CraftingRequestsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CraftingRequestsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<CraftingRequestListItemDto>>> GetRequests(CancellationToken ct)
    {
        var items = await _db.CraftingRequests
            .AsNoTracking()
            .Include(x => x.Blueprint)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedCrafterProfile)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CraftingRequestListItemDto
            {
                Id = x.Id,
                BlueprintId = x.BlueprintId,
                BlueprintName = x.Blueprint != null ? x.Blueprint.BlueprintName : string.Empty,
                CraftedItemName = x.Blueprint != null ? x.Blueprint.CraftedItemName : string.Empty,
                RequesterUsername = x.RequesterProfile != null ? x.RequesterProfile.Username : string.Empty,
                AssignedCrafterUsername = x.AssignedCrafterProfile != null ? x.AssignedCrafterProfile.Username : null,
                QuantityRequested = x.QuantityRequested,
                Priority = x.Priority.ToString(),
                Status = x.Status.ToString(),
                DeliveryLocation = x.DeliveryLocation,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateCraftingRequest([FromBody] CreateCraftingRequestDto request, CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(discordId))
            return Unauthorized();

        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        if (profile is null)
            return Unauthorized();

        var blueprint = await _db.Blueprints.FirstOrDefaultAsync(x => x.Id == request.BlueprintId, ct);
        if (blueprint is null)
        {
            blueprint = new Blueprint
            {
                Id = request.BlueprintId,
                Slug = request.BlueprintId.ToString(),
                BlueprintName = "Wiki Blueprint",
                CraftedItemName = "Unknown",
                Category = "Unknown",
                IsInGameAvailable = true,
                DataConfidence = "WikiAPI",
                WikiUuid = request.BlueprintId.ToString()
            };
            _db.Blueprints.Add(blueprint);
            await _db.SaveChangesAsync(ct);
        }

        var craftingRequest = new CraftingRequest
        {
            BlueprintId = blueprint.Id,
            RequesterProfileId = profile.Id,
            QuantityRequested = request.QuantityRequested <= 0 ? 1 : request.QuantityRequested,
            Priority = request.Priority,
            DeliveryLocation = request.DeliveryLocation,
            RewardOffered = request.RewardOffered,
            RequiredBy = request.RequiredBy,
            Notes = request.Notes
        };

        _db.CraftingRequests.Add(craftingRequest);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Crafting request created.", requestId = craftingRequest.Id });
    }

    [HttpGet("procurement-requests")]
    public async Task<ActionResult<IReadOnlyList<MaterialProcurementRequestListItemDto>>> GetProcurementRequests(CancellationToken ct)
    {
        var items = await _db.MaterialProcurementRequests
            .AsNoTracking()
            .Include(x => x.Material)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MaterialProcurementRequestListItemDto
            {
                Id = x.Id,
                MaterialId = x.MaterialId,
                LinkedCraftingRequestId = x.LinkedCraftingRequestId,
                MaterialName = x.Material != null ? x.Material.Name : string.Empty,
                QuantityRequested = x.QuantityRequested,
                QuantityDelivered = x.QuantityDelivered,
                PreferredForm = x.PreferredForm.ToString(),
                Priority = x.Priority.ToString(),
                Status = x.Status.ToString(),
                DeliveryLocation = x.DeliveryLocation,
                NumberOfHelpersNeeded = x.NumberOfHelpersNeeded,
                RewardOffered = x.RewardOffered,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("procurement-requests")]
    public async Task<IActionResult> CreateProcurementRequest([FromBody] CreateMaterialProcurementRequestDto request, CancellationToken ct)
    {
        var material = await _db.Materials.FirstOrDefaultAsync(x => x.Id == request.MaterialId, ct);
        if (material is null)
        {
            material = new Material
            {
                Id = request.MaterialId,
                Name = "Unknown Material",
                Slug = request.MaterialId.ToString(),
                MaterialType = "Resource",
                Tier = string.Empty
            };
            _db.Materials.Add(material);
            await _db.SaveChangesAsync(ct);
        }

        if (request.LinkedCraftingRequestId.HasValue)
        {
            var linkedExists = await _db.CraftingRequests.AnyAsync(x => x.Id == request.LinkedCraftingRequestId.Value, ct);
            if (!linkedExists)
                return NotFound(new { error = "Linked crafting request not found." });
        }

        var procurementRequest = new MaterialProcurementRequest
        {
            MaterialId = request.MaterialId,
            LinkedCraftingRequestId = request.LinkedCraftingRequestId,
            QuantityRequested = request.QuantityRequested,
            PreferredForm = request.PreferredForm,
            Priority = request.Priority,
            DeliveryLocation = request.DeliveryLocation,
            NumberOfHelpersNeeded = request.NumberOfHelpersNeeded,
            RewardOffered = request.RewardOffered,
            Notes = request.Notes
        };

        _db.MaterialProcurementRequests.Add(procurementRequest);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Material procurement request created.", requestId = procurementRequest.Id });
    }
}
