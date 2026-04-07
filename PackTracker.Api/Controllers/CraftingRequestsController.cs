using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PackTracker.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<CraftingRequestsController> _logger;

    public CraftingRequestsController(AppDbContext db, ILogger<CraftingRequestsController> logger)
    {
        _db = db;
        _logger = logger;
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
                RewardOffered = x.RewardOffered,
                RequiredBy = x.RequiredBy,
                Notes = x.Notes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        _logger.LogInformation("Crafting requests listed. Count={Count}", items.Count);
        return Ok(items);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateCraftingRequest([FromBody] CreateCraftingRequestDto request, CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(discordId))
        {
            _logger.LogWarning("CreateCraftingRequest rejected — no DiscordId claim.");
            return Unauthorized();
        }

        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        if (profile is null)
        {
            _logger.LogWarning("CreateCraftingRequest rejected — profile not found for DiscordId={DiscordId}.", discordId);
            return Unauthorized();
        }

        var blueprint = await _db.Blueprints.FirstOrDefaultAsync(x => x.Id == request.BlueprintId, ct);
        if (blueprint is null)
        {
            _logger.LogInformation("Blueprint {BlueprintId} not found — creating wiki placeholder.", request.BlueprintId);
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

        _logger.LogInformation(
            "Crafting request created. RequestId={RequestId} BlueprintId={BlueprintId} Requester={Username} Priority={Priority}",
            craftingRequest.Id, blueprint.Id, profile.Username, request.Priority);

        return Ok(new { message = "Crafting request created.", requestId = craftingRequest.Id });
    }

    [HttpGet("procurement-requests")]
    public async Task<ActionResult<IReadOnlyList<MaterialProcurementRequestListItemDto>>> GetProcurementRequests(CancellationToken ct)
    {
        var items = await _db.MaterialProcurementRequests
            .AsNoTracking()
            .Include(x => x.Material)
            .Include(x => x.AssignedToProfile)
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
                AssignedToUsername = x.AssignedToProfile != null ? x.AssignedToProfile.Username : null,
                Notes = x.Notes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        _logger.LogInformation("Procurement requests listed. Count={Count}", items.Count);
        return Ok(items);
    }

    [HttpPost("procurement-requests")]
    public async Task<IActionResult> CreateProcurementRequest([FromBody] CreateMaterialProcurementRequestDto request, CancellationToken ct)
    {
        var material = await _db.Materials.FirstOrDefaultAsync(x => x.Id == request.MaterialId, ct);
        if (material is null)
        {
            _logger.LogInformation("Material {MaterialId} not found — creating placeholder.", request.MaterialId);
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
            {
                _logger.LogWarning("CreateProcurementRequest — linked crafting request {LinkedId} not found.", request.LinkedCraftingRequestId);
                return NotFound(new { error = "Linked crafting request not found." });
            }
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

        _logger.LogInformation(
            "Procurement request created. RequestId={RequestId} MaterialId={MaterialId} Qty={Qty} Priority={Priority}",
            procurementRequest.Id, request.MaterialId, request.QuantityRequested, request.Priority);

        return Ok(new { message = "Material procurement request created.", requestId = procurementRequest.Id });
    }

    // ─── CRAFTING: UPDATE STATUS ────────────────────────────────────────────────
    [HttpPatch("requests/{id:guid}/status")]
    public async Task<IActionResult> UpdateCraftingRequestStatus(Guid id, [FromBody] UpdateRequestStatusDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<RequestStatus>(dto.Status, ignoreCase: true, out var newStatus))
        {
            _logger.LogWarning("UpdateCraftingRequestStatus — invalid status value '{Status}'.", dto.Status);
            return BadRequest(new { error = $"Unknown status '{dto.Status}'. Valid values: {string.Join(", ", Enum.GetNames<RequestStatus>())}" });
        }

        var request = await _db.CraftingRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
        {
            _logger.LogWarning("UpdateCraftingRequestStatus — request {RequestId} not found.", id);
            return NotFound(new { error = "Crafting request not found." });
        }

        var previous = request.Status;
        request.Status = newStatus;
        request.UpdatedAt = DateTime.UtcNow;
        if (newStatus == RequestStatus.Completed)
            request.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Crafting request status changed. RequestId={RequestId} Previous={Previous} New={New} User={User}",
            id, previous, newStatus, User.Identity?.Name ?? "unknown");

        return Ok(new { message = $"Crafting request status updated to {newStatus}.", requestId = id });
    }

    // ─── CRAFTING: ASSIGN SELF ──────────────────────────────────────────────────
    [HttpPatch("requests/{id:guid}/assign")]
    public async Task<IActionResult> AssignCraftingRequestToSelf(Guid id, CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(discordId))
        {
            _logger.LogWarning("AssignCraftingRequest rejected — no DiscordId claim.");
            return Unauthorized();
        }

        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        if (profile is null)
        {
            _logger.LogWarning("AssignCraftingRequest rejected — profile not found for DiscordId={DiscordId}.", discordId);
            return Unauthorized();
        }

        var request = await _db.CraftingRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
        {
            _logger.LogWarning("AssignCraftingRequest — request {RequestId} not found.", id);
            return NotFound(new { error = "Crafting request not found." });
        }

        request.AssignedCrafterProfileId = profile.Id;
        request.Status = RequestStatus.InProgress;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Crafting request assigned. RequestId={RequestId} AssignedTo={Username}",
            id, profile.Username);

        return Ok(new { message = $"Crafting request assigned to {profile.Username}.", requestId = id });
    }

    // ─── PROCUREMENT: UPDATE STATUS ─────────────────────────────────────────────
    [HttpPatch("procurement-requests/{id:guid}/status")]
    public async Task<IActionResult> UpdateProcurementRequestStatus(Guid id, [FromBody] UpdateRequestStatusDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<RequestStatus>(dto.Status, ignoreCase: true, out var newStatus))
        {
            _logger.LogWarning("UpdateProcurementRequestStatus — invalid status value '{Status}'.", dto.Status);
            return BadRequest(new { error = $"Unknown status '{dto.Status}'. Valid values: {string.Join(", ", Enum.GetNames<RequestStatus>())}" });
        }

        var request = await _db.MaterialProcurementRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
        {
            _logger.LogWarning("UpdateProcurementRequestStatus — request {RequestId} not found.", id);
            return NotFound(new { error = "Procurement request not found." });
        }

        var previous = request.Status;
        request.Status = newStatus;
        request.UpdatedAt = DateTime.UtcNow;
        if (newStatus == RequestStatus.Completed)
            request.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Procurement request status changed. RequestId={RequestId} Previous={Previous} New={New} User={User}",
            id, previous, newStatus, User.Identity?.Name ?? "unknown");

        return Ok(new { message = $"Procurement request status updated to {newStatus}.", requestId = id });
    }

    // ─── PROCUREMENT: CLAIM ─────────────────────────────────────────────────────
    [HttpPatch("procurement-requests/{id:guid}/claim")]
    public async Task<IActionResult> ClaimProcurementRequest(Guid id, CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(discordId))
        {
            _logger.LogWarning("ClaimProcurementRequest rejected — no DiscordId claim.");
            return Unauthorized();
        }

        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        if (profile is null)
        {
            _logger.LogWarning("ClaimProcurementRequest rejected — profile not found for DiscordId={DiscordId}.", discordId);
            return Unauthorized();
        }

        var request = await _db.MaterialProcurementRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
        {
            _logger.LogWarning("ClaimProcurementRequest — request {RequestId} not found.", id);
            return NotFound(new { error = "Procurement request not found." });
        }

        request.AssignedToProfileId = profile.Id;
        request.Status = RequestStatus.InProgress;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Procurement request claimed. RequestId={RequestId} ClaimedBy={Username}",
            id, profile.Username);

        return Ok(new { message = $"Procurement request claimed by {profile.Username}.", requestId = id });
    }
}
