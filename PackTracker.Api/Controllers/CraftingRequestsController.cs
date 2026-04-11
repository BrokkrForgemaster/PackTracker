using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Api.Hubs;
using RequestsHub = PackTracker.Api.Hubs.RequestsHub;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

/// <summary>
/// Handles crafting and procurement request workflows.
/// </summary>
[ApiController]
[Route("api/v1/crafting")]
[Authorize]
public class CraftingRequestsController : ControllerBase
{
    private static readonly HashSet<string> ElevatedRequestRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Captain",
        "Fleet Commander",
        "Armor",
        "Hand of the Clan",
        "Clan Warlord"
    };

    #region Fields

    private readonly AppDbContext _db;
    private readonly ILogger<CraftingRequestsController> _logger;
    private readonly IHubContext<RequestsHub> _hub;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CraftingRequestsController"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="hub">The SignalR hub context used for realtime request updates.</param>
    public CraftingRequestsController(
        AppDbContext db,
        ILogger<CraftingRequestsController> logger,
        IHubContext<RequestsHub> hub)
    {
        _db = db;
        _logger = logger;
        _hub = hub;
    }

    #endregion

    #region Crafting Requests

    /// <summary>
    /// Creates a new crafting request for the current authenticated user.
    /// </summary>
    /// <param name="request">The crafting request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created request identifier.</returns>
    [HttpPost("requests")]
    public async Task<IActionResult> CreateCraftingRequest(
        [FromBody] CreateCraftingRequestDto request,
        CancellationToken ct)
    {
        if (request is null)
        {
            _logger.LogWarning("CreateCraftingRequest received null payload.");
            return BadRequest("Request payload is required.");
        }

        var profile = await GetCurrentProfile(ct);
        if (profile is null)
        {
            _logger.LogWarning("CreateCraftingRequest unauthorized. No matching profile for current user.");
            return Unauthorized();
        }

        var blueprint = await _db.Blueprints
            .FirstOrDefaultAsync(x => x.Id == request.BlueprintId, ct);

        if (blueprint is null)
        {
            _logger.LogWarning(
                "CreateCraftingRequest failed. Blueprint {BlueprintId} was not found for user {DiscordId}.",
                request.BlueprintId,
                profile.DiscordId);

            return NotFound(new { error = "Blueprint not found." });
        }

        // Repair stale placeholder names written by the seed service
        if (!string.IsNullOrWhiteSpace(request.CraftedItemName))
        {
            var itemName = request.CraftedItemName.Trim();
            var needsUpdate = string.IsNullOrWhiteSpace(blueprint.CraftedItemName)
                           || blueprint.CraftedItemName == "Unknown"
                           || blueprint.BlueprintName == "Wiki Blueprint";

            if (needsUpdate)
            {
                blueprint.CraftedItemName = itemName;
                blueprint.BlueprintName = $"{itemName} Blueprint";
            }
        }

        var craftingRequest = new CraftingRequest
        {
            BlueprintId = request.BlueprintId,
            ItemName = !string.IsNullOrWhiteSpace(request.CraftedItemName) ? request.CraftedItemName.Trim() : null,
            RequesterProfileId = profile.Id,
            QuantityRequested = request.QuantityRequested <= 0 ? 1 : request.QuantityRequested,
            MinimumQuality = request.MinimumQuality <= 0 ? 1 : request.MinimumQuality,
            Priority = request.Priority,
            MaterialSupplyMode = request.MaterialSupplyMode,
            DeliveryLocation = request.DeliveryLocation,
            RewardOffered = request.RewardOffered,
            RequiredBy = request.RequiredBy,
            Notes = request.Notes,
            RequesterTimeZoneDisplayName = string.IsNullOrWhiteSpace(request.RequesterTimeZoneDisplayName)
                ? null
                : request.RequesterTimeZoneDisplayName.Trim(),
            RequesterUtcOffsetMinutes = request.RequesterUtcOffsetMinutes,
            Status = RequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.CraftingRequests.Add(craftingRequest);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Crafting request created. RequestId={RequestId} BlueprintId={BlueprintId} RequesterProfileId={RequesterProfileId}",
            craftingRequest.Id,
            craftingRequest.BlueprintId,
            craftingRequest.RequesterProfileId);

        await BroadcastAsync("CraftingRequestCreated", craftingRequest.Id, ct);

        return Ok(new
        {
            message = "Crafting request created.",
            requestId = craftingRequest.Id
        });
    }

    /// <summary>
    /// Returns active crafting requests (excluding Cancelled and Completed).
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of crafting request list items.</returns>
    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<CraftingRequestListItemDto>>> GetCraftingRequests(CancellationToken ct)
    {
        var currentUsername = User.Identity?.Name ?? string.Empty;

        var requests = await _db.CraftingRequests
            .AsNoTracking()
            .Include(x => x.Blueprint)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedCrafterProfile)
            .OrderByDescending(x => x.CreatedAt)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            // Open requests are visible to all; assigned requests are only visible to requester or crafter
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfile!.Username == currentUsername
                     || x.AssignedCrafterProfile!.Username == currentUsername)
            .ToListAsync(ct);

        var result = new List<CraftingRequestListItemDto>();

        foreach (var r in requests)
        {
            var materials = await _db.BlueprintRecipeMaterials
                .AsNoTracking()
                .Where(x => _db.BlueprintRecipes.Any(rec => rec.BlueprintId == r.BlueprintId && rec.Id == x.BlueprintRecipeId))
                .Include(x => x.Material)
                .Select(x => new BlueprintRecipeMaterialDto
                {
                    MaterialId = x.MaterialId,
                    MaterialName = x.Material != null ? x.Material.Name : "Unknown",
                    MaterialType = x.Material != null ? x.Material.MaterialType : string.Empty,
                    Tier = x.Material != null ? x.Material.Tier : string.Empty,
                    QuantityRequired = x.QuantityRequired,
                    Unit = x.Unit,
                    SourceType = x.Material != null ? x.Material.SourceType.ToString() : "Unknown"
                })
                .ToListAsync(ct);

            result.Add(new CraftingRequestListItemDto
            {
                Id = r.Id,
                BlueprintId = r.BlueprintId,
                BlueprintName = !string.IsNullOrWhiteSpace(r.ItemName)
                    ? r.ItemName
                    : (!string.IsNullOrWhiteSpace(r.Blueprint?.CraftedItemName) && r.Blueprint.CraftedItemName != "Unknown"
                        ? r.Blueprint.CraftedItemName
                        : r.Blueprint?.BlueprintName?.Replace(" Blueprint", "")) ?? "Unknown",
                CraftedItemName = r.Blueprint?.CraftedItemName ?? "Unknown Item",
                RequesterUsername = r.RequesterProfile?.Username ?? "Unknown",
                RequesterDisplayName = r.RequesterProfile?.DiscordDisplayName ?? r.RequesterProfile?.Username ?? "Unknown",
                AssignedCrafterUsername = r.AssignedCrafterProfile?.Username,
                QuantityRequested = r.QuantityRequested,
                MinimumQuality = r.MinimumQuality,
                RefusalReason = r.RefusalReason,
                Priority = r.Priority.ToString(),
                Status = r.Status.ToString(),
                MaterialSupplyMode = r.MaterialSupplyMode.ToString(),
                DeliveryLocation = r.DeliveryLocation,
                RewardOffered = r.RewardOffered,
                RequiredBy = r.RequiredBy,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt,
                RequesterTimeZoneDisplayName = r.RequesterTimeZoneDisplayName,
                RequesterUtcOffsetMinutes = r.RequesterUtcOffsetMinutes,
                Materials = materials
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns all comments for a crafting request.
    /// </summary>
    /// <param name="id">The crafting request identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of comments.</returns>
    [HttpGet("requests/{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<RequestCommentDto>>> GetComments(Guid id, CancellationToken ct)
    {
        var requestExists = await _db.CraftingRequests.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!requestExists)
            return NotFound(new { error = "Crafting request not found." });

        var comments = await _db.RequestComments
            .AsNoTracking()
            .Where(x => x.RequestId == id)
            .Include(x => x.AuthorProfile)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new RequestCommentDto
            {
                Id = x.Id,
                RequestId = x.RequestId,
                AuthorUsername = x.AuthorProfile != null ? x.AuthorProfile.Username : "Unknown",
                Content = x.Content,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(comments);
    }

    /// <summary>
    /// Adds a comment to a crafting request.
    /// </summary>
    /// <param name="id">The crafting request identifier.</param>
    /// <param name="dto">The comment payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A success response with the new comment identifier.</returns>
    [HttpPost("requests/{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddRequestCommentDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "Comment content is required." });

        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var requestExists = await _db.CraftingRequests.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!requestExists)
            return NotFound(new { error = "Crafting request not found." });

        var comment = new RequestComment
        {
            RequestId = id,
            AuthorProfileId = profile.Id,
            Content = dto.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.RequestComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Comment added to crafting request. RequestId={RequestId} AuthorProfileId={AuthorProfileId} CommentId={CommentId}",
            id,
            profile.Id,
            comment.Id);

        await BroadcastAsync("RequestCommentAdded", id, ct);

        return Ok(new { message = "Comment added.", commentId = comment.Id });
    }

    /// <summary>
    /// Assigns the current user as the crafter for an open request.
    /// </summary>
    /// <param name="id">The crafting request identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A success response.</returns>
    [HttpPatch("requests/{id:guid}/assign")]
    public async Task<IActionResult> AssignToSelf(Guid id, CancellationToken ct)
    {
        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var request = await _db.CraftingRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Crafting request not found." });

        if (request.Status != RequestStatus.Open)
            return BadRequest(new { error = "Only open requests can be assigned." });

        request.AssignedCrafterProfileId = profile.Id;
        request.Status = RequestStatus.Accepted;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await BroadcastAsync("CraftingRequestUpdated", id, ct);

        return Ok(new { message = "Request assigned.", requestId = id });
    }

    /// <summary>
    /// Refuses a crafting request with a reason.
    /// </summary>
    /// <param name="id">The crafting request identifier.</param>
    /// <param name="dto">The refusal payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A success response.</returns>
    [HttpPatch("requests/{id:guid}/refuse")]
    public async Task<IActionResult> RefuseRequest(Guid id, [FromBody] RefuseRequestDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { error = "Refusal reason is required." });

        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var request = await _db.CraftingRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Crafting request not found." });

        request.Status = RequestStatus.Refused;
        request.RefusalReason = dto.Reason.Trim();
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await BroadcastAsync("CraftingRequestUpdated", id, ct);

        return Ok(new { message = "Request refused.", requestId = id });
    }

    /// <summary>
    /// Updates the status of a crafting request.
    /// </summary>
    /// <param name="id">The crafting request identifier.</param>
    /// <param name="dto">The status update payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A success response with old and new status.</returns>
    [HttpPatch("requests/{id:guid}/status")]
    public async Task<IActionResult> UpdateCraftingRequestStatus(Guid id, [FromBody] UpdateRequestStatusDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Status))
            return BadRequest(new { error = "Status is required." });

        if (!Enum.TryParse<RequestStatus>(dto.Status, true, out var parsedStatus))
            return BadRequest(new { error = $"Invalid status '{dto.Status}'." });

        var request = await _db.CraftingRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Crafting request not found." });

        if (parsedStatus == RequestStatus.Cancelled)
        {
            var profile = await GetCurrentProfile(ct);
            if (profile is null)
                return Unauthorized();

            if (!CanManageRequest(profile, request.RequesterProfileId))
                return StatusCode(403, new { error = "Only the creator or authorized leadership may cancel this request." });
        }

        var previous = request.Status;
        request.Status = parsedStatus;
        request.UpdatedAt = DateTime.UtcNow;

        if (parsedStatus == RequestStatus.Completed)
            request.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await BroadcastAsync("CraftingRequestUpdated", id, ct);

        return Ok(new { message = "Status updated.", requestId = id, previousStatus = previous.ToString(), newStatus = parsedStatus.ToString() });
    }

    /// <summary>
    /// Cancels a crafting request. The creator or authorized leadership may remove it from active queues.
    /// </summary>
    [HttpDelete("requests/{id:guid}")]
    public async Task<IActionResult> DeleteCraftingRequest(Guid id, CancellationToken ct)
    {
        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var request = await _db.CraftingRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Crafting request not found." });

        if (!CanManageRequest(profile, request.RequesterProfileId))
            return StatusCode(403, new { error = "Only the creator or authorized leadership may remove this request." });

        if (request.Status == RequestStatus.Cancelled)
            return BadRequest(new { error = "Request is already cancelled." });

        request.Status = RequestStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await BroadcastAsync("CraftingRequestUpdated", id, ct);

        return Ok(new { message = "Crafting request removed.", requestId = id });
    }

    #endregion

    #region Procurement Requests

    /// <summary>
    /// Returns active procurement requests visible to the current user.
    /// </summary>
    [HttpGet("procurement-requests")]
    public async Task<ActionResult<IReadOnlyList<MaterialProcurementRequestListItemDto>>> GetProcurementRequests(CancellationToken ct)
    {
        var currentUsername = User.Identity?.Name ?? string.Empty;

        var requests = await _db.MaterialProcurementRequests
            .AsNoTracking()
            .Include(x => x.Material)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedToProfile)
            .OrderByDescending(x => x.CreatedAt)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfile!.Username == currentUsername
                     || x.AssignedToProfile!.Username == currentUsername)
            .Select(x => new MaterialProcurementRequestListItemDto
            {
                Id = x.Id,
                MaterialId = x.MaterialId,
                LinkedCraftingRequestId = x.LinkedCraftingRequestId,
                MaterialName = x.Material != null ? x.Material.Name : "Unknown",
                RequesterUsername = x.RequesterProfile != null ? x.RequesterProfile.Username : "Unknown",
                RequesterDisplayName = x.RequesterProfile != null
                    ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username)
                    : "Unknown",
                AssignedToUsername = x.AssignedToProfile != null ? x.AssignedToProfile.Username : null,
                QuantityRequested = x.QuantityRequested,
                QuantityDelivered = x.QuantityDelivered,
                MinimumQuality = x.MinimumQuality,
                PreferredForm = x.PreferredForm.ToString(),
                Priority = x.Priority.ToString(),
                Status = x.Status.ToString(),
                DeliveryLocation = x.DeliveryLocation,
                NumberOfHelpersNeeded = x.NumberOfHelpersNeeded,
                RewardOffered = x.RewardOffered,
                Notes = x.Notes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(requests);
    }

    /// <summary>
    /// Returns all comments for a procurement request.
    /// </summary>
    [HttpGet("procurement-requests/{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<RequestCommentDto>>> GetProcurementComments(Guid id, CancellationToken ct)
    {
        var requestExists = await _db.MaterialProcurementRequests.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!requestExists)
            return NotFound(new { error = "Procurement request not found." });

        var comments = await _db.RequestComments
            .AsNoTracking()
            .Where(x => x.RequestId == id)
            .Include(x => x.AuthorProfile)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new RequestCommentDto
            {
                Id = x.Id,
                RequestId = x.RequestId,
                AuthorUsername = x.AuthorProfile != null ? x.AuthorProfile.Username : "Unknown",
                Content = x.Content,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(comments);
    }

    /// <summary>
    /// Adds a comment to a procurement request.
    /// </summary>
    [HttpPost("procurement-requests/{id:guid}/comments")]
    public async Task<IActionResult> AddProcurementComment(Guid id, [FromBody] AddRequestCommentDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "Comment content is required." });

        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var requestExists = await _db.MaterialProcurementRequests.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!requestExists)
            return NotFound(new { error = "Procurement request not found." });

        var comment = new RequestComment
        {
            RequestId = id,
            AuthorProfileId = profile.Id,
            Content = dto.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.RequestComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Comment added.", commentId = comment.Id });
    }

    /// <summary>
    /// Assigns the current user to a procurement request.
    /// </summary>
    [HttpPatch("procurement-requests/{id:guid}/claim")]
    public async Task<IActionResult> ClaimProcurementRequest(Guid id, CancellationToken ct)
    {
        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var request = await _db.MaterialProcurementRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Procurement request not found." });

        if (request.Status != RequestStatus.Open)
            return BadRequest(new { error = "Only open requests can be claimed." });

        request.AssignedToProfileId = profile.Id;
        request.Status = RequestStatus.Accepted;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await BroadcastAsync("ProcurementUpdated", id, ct);

        return Ok(new { message = "Request claimed.", requestId = id });
    }

    /// <summary>
    /// Refuses a procurement request with a reason.
    /// </summary>
    [HttpPatch("procurement-requests/{id:guid}/refuse")]
    public async Task<IActionResult> RefuseProcurementRequest(Guid id, [FromBody] RefuseRequestDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { error = "Refusal reason is required." });

        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var request = await _db.MaterialProcurementRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Procurement request not found." });

        request.Status = RequestStatus.Refused;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await BroadcastAsync("ProcurementUpdated", id, ct);

        return Ok(new { message = "Request refused.", requestId = id });
    }

    /// <summary>
    /// Creates a new procurement request for the current authenticated user.
    /// </summary>
    /// <param name="request">The procurement request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created procurement request identifier.</returns>
    [HttpPost("procurement-requests")]
    public async Task<IActionResult> CreateProcurementRequest(
        [FromBody] CreateMaterialProcurementRequestDto request,
        CancellationToken ct)
    {
        if (request is null)
        {
            _logger.LogWarning("CreateProcurementRequest received null payload.");
            return BadRequest("Request payload is required.");
        }

        var profile = await GetCurrentProfile(ct);
        if (profile is null)
        {
            _logger.LogWarning("CreateProcurementRequest unauthorized. No matching profile for current user.");
            return Unauthorized();
        }

        var wikiUuidString = request.MaterialId.ToString();
        var material = await _db.Materials
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.MaterialId
                                   || x.WikiUuid == wikiUuidString
                                   || (!string.IsNullOrWhiteSpace(request.MaterialName)
                                       && x.Name == request.MaterialName), ct);

        if (material is null)
        {
            _logger.LogWarning(
                "CreateProcurementRequest failed. Material '{MaterialName}' ({MaterialId}) not found for user {DiscordId}.",
                request.MaterialName,
                request.MaterialId,
                profile.DiscordId);

            return NotFound(new { error = $"Material '{request.MaterialName ?? wikiUuidString}' not found in database." });
        }

        if (request.LinkedCraftingRequestId.HasValue)
        {
            var linkedExists = await _db.CraftingRequests
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.LinkedCraftingRequestId.Value, ct);

            if (!linkedExists)
            {
                _logger.LogWarning(
                    "CreateProcurementRequest failed. Linked crafting request {LinkedCraftingRequestId} not found.",
                    request.LinkedCraftingRequestId.Value);

                return NotFound(new { error = "Linked crafting request not found." });
            }
        }

        var entity = new MaterialProcurementRequest
        {
            LinkedCraftingRequestId = request.LinkedCraftingRequestId,
            MaterialId = material.Id,
            RequesterProfileId = profile.Id,
            QuantityRequested = request.QuantityRequested,
            QuantityDelivered = 0,
            MinimumQuality = request.MinimumQuality <= 0 ? 1 : request.MinimumQuality,
            PreferredForm = request.PreferredForm,
            Priority = request.Priority,
            Status = RequestStatus.Open,
            DeliveryLocation = request.DeliveryLocation,
            NumberOfHelpersNeeded = request.NumberOfHelpersNeeded,
            RewardOffered = request.RewardOffered,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.MaterialProcurementRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Procurement request created. RequestId={RequestId} MaterialId={MaterialId} RequesterProfileId={RequesterProfileId}",
            entity.Id,
            entity.MaterialId,
            entity.RequesterProfileId);

        await BroadcastAsync("ProcurementRequestCreated", entity.Id, ct);

        return Ok(new
        {
            message = "Procurement request created.",
            requestId = entity.Id
        });
    }

    #endregion

    #region Status Updates

    /// <summary>
    /// Updates the status of a procurement request.
    /// </summary>
    /// <param name="id">The procurement request identifier.</param>
    /// <param name="dto">The status update payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A success response if the update completed.</returns>
    [HttpPatch("procurement-requests/{id:guid}/status")]
    public async Task<IActionResult> UpdateProcurementStatus(
        Guid id,
        [FromBody] UpdateRequestStatusDto dto,
        CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Status))
        {
            _logger.LogWarning("UpdateProcurementStatus received invalid payload for RequestId={RequestId}.", id);
            return BadRequest(new { error = "Status is required." });
        }

        if (!Enum.TryParse<RequestStatus>(dto.Status, true, out var parsedStatus))
        {
            _logger.LogWarning(
                "UpdateProcurementStatus received invalid status '{Status}' for RequestId={RequestId}.",
                dto.Status,
                id);

            return BadRequest(new
            {
                error = $"Invalid status '{dto.Status}'."
            });
        }

        var entity = await _db.MaterialProcurementRequests
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedToProfile)
            .Include(x => x.Material)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity is null)
        {
            _logger.LogWarning("UpdateProcurementStatus failed. RequestId={RequestId} not found.", id);
            return NotFound(new { error = "Procurement request not found." });
        }

        if (parsedStatus == RequestStatus.Cancelled)
        {
            var profile = await GetCurrentProfile(ct);
            if (profile is null)
                return Unauthorized();

            if (!CanManageRequest(profile, entity.RequesterProfileId))
                return StatusCode(403, new { error = "Only the creator or authorized leadership may cancel this request." });
        }

        var previousStatus = entity.Status;

        entity.Status = parsedStatus;
        entity.UpdatedAt = DateTime.UtcNow;

        if (parsedStatus == RequestStatus.Completed)
        {
            entity.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Procurement status updated. RequestId={RequestId} PreviousStatus={PreviousStatus} NewStatus={NewStatus}",
            entity.Id,
            previousStatus,
            entity.Status);

        await BroadcastAsync("ProcurementUpdated", entity.Id, ct);

        return Ok(new
        {
            message = "Procurement request status updated.",
            requestId = entity.Id,
            previousStatus = previousStatus.ToString(),
            newStatus = entity.Status.ToString()
        });
    }

    /// <summary>
    /// Cancels a procurement request. The creator or authorized leadership may remove it from active queues.
    /// </summary>
    [HttpDelete("procurement-requests/{id:guid}")]
    public async Task<IActionResult> DeleteProcurementRequest(Guid id, CancellationToken ct)
    {
        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var entity = await _db.MaterialProcurementRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(new { error = "Procurement request not found." });

        if (!CanManageRequest(profile, entity.RequesterProfileId))
            return StatusCode(403, new { error = "Only the creator or authorized leadership may remove this request." });

        if (entity.Status == RequestStatus.Cancelled)
            return BadRequest(new { error = "Request is already cancelled." });

        entity.Status = RequestStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await BroadcastAsync("ProcurementUpdated", id, ct);

        return Ok(new { message = "Procurement request removed.", requestId = id });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Resolves the current authenticated user's profile from the Discord name identifier claim.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching profile, or null if not found.</returns>
    private async Task<Profile?> GetCurrentProfile(CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(discordId))
        {
            _logger.LogWarning("No Discord name identifier claim was present on the current principal.");
            return null;
        }

        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);

        if (profile is null)
        {
            _logger.LogWarning("No profile found for DiscordId={DiscordId}.", discordId);
        }

        return profile;
    }

    /// <summary>
    /// Broadcasts a request-related SignalR event to all connected clients.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="id">The affected request identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    private async Task BroadcastAsync(string eventName, Guid id, CancellationToken ct)
    {
        _logger.LogDebug(
            "Broadcasting SignalR event. EventName={EventName} EntityId={EntityId}",
            eventName,
            id);

        await _hub.Clients.All.SendAsync(eventName, id, ct);
    }

    private bool CanManageRequest(Profile profile, Guid? creatorProfileId) =>
        creatorProfileId.HasValue && profile.Id == creatorProfileId.Value
        || UserHasElevatedRequestRole(profile.DiscordRank);

    private bool UserHasElevatedRequestRole(string? profileRole) =>
        ElevatedRequestRoles.Contains(profileRole ?? string.Empty)
        || ElevatedRequestRoles.Any(User.IsInRole);

    #endregion
}
