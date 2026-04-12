using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Api.Hubs;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;
using PackTracker.Infrastructure.Persistence;
using HubsRequestsHub = PackTracker.Api.Hubs.RequestsHub;

namespace PackTracker.Api.Controllers;

/// <summary>
/// Handles general assistance request workflows for the Assistance Hub feature.
/// </summary>
[ApiController]
[Route("api/v1/requests")]
[Authorize]
public class AssistanceRequestsController : ControllerBase
{
    #region Fields

    private readonly AppDbContext _db;
    private readonly ILogger<AssistanceRequestsController> _logger;
    private readonly IHubContext<HubsRequestsHub> _hub;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="AssistanceRequestsController"/> class.
    /// </summary>
    public AssistanceRequestsController(
        AppDbContext db,
        ILogger<AssistanceRequestsController> logger,
        IHubContext<HubsRequestsHub> hub)
    {
        _db = db;
        _logger = logger;
        _hub = hub;
    }

    #endregion

    #region Endpoints

    /// <summary>
    /// Returns active assistance requests visible to the current user.
    /// Excludes Cancelled and Completed. Returns Open requests to all users,
    /// and non-open requests only if the user is the requester or assignee.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssistanceRequestDto>>> GetRequests(
        [FromQuery] RequestKind? kind,
        [FromQuery] RequestStatus? status,
        CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var currentProfile = await _db.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);

        var currentProfileId = currentProfile?.Id ?? Guid.Empty;

        var query = _db.AssistanceRequests
            .AsNoTracking()
            .Include(x => x.CreatedByProfile)
            .Include(x => x.AssignedToProfile)
            .Where(x => x.Status == RequestStatus.Open
                     || x.CreatedByProfileId == currentProfileId
                     || x.AssignedToProfileId == currentProfileId);

        if (kind.HasValue)
            query = query.Where(x => x.Kind == kind.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        else
            query = query.Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed);

        var requests = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AssistanceRequestDto
            {
                Id = x.Id,
                Kind = x.Kind,
                Title = x.Title,
                Description = x.Description,
                Priority = x.Priority,
                Status = x.Status.ToString(),
                CreatedByUsername = x.CreatedByProfile != null ? x.CreatedByProfile.Username : "Unknown",
                CreatedByDisplayName = x.CreatedByProfile != null
                    ? (x.CreatedByProfile.DiscordDisplayName ?? x.CreatedByProfile.Username)
                    : "Unknown",
                AssignedToUsername = x.AssignedToProfile != null ? x.AssignedToProfile.Username : null,
                MaterialName = x.MaterialName,
                QuantityNeeded = x.QuantityNeeded,
                MeetingLocation = x.MeetingLocation,
                RewardOffered = x.RewardOffered,
                NumberOfHelpersNeeded = x.NumberOfHelpersNeeded,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(requests);
    }

    /// <summary>
    /// Creates a new assistance request for the current authenticated user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRequest(
        [FromBody] RequestCreateDto dto,
        CancellationToken ct)
    {
        if (dto is null)
        {
            _logger.LogWarning("CreateRequest received null payload.");
            return BadRequest(new { error = "Request payload is required." });
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { error = "Title is required." });

        var profile = await GetCurrentProfile(ct);
        if (profile is null)
        {
            _logger.LogWarning("CreateRequest unauthorized. No matching profile for current user.");
            return Unauthorized();
        }

        var entity = new AssistanceRequest
        {
            Kind = dto.Kind,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Priority = dto.Priority,
            Status = RequestStatus.Open,
            CreatedByProfileId = profile.Id,
            MaterialName = dto.MaterialName?.Trim(),
            QuantityNeeded = dto.QuantityNeeded,
            MeetingLocation = dto.MeetingLocation?.Trim(),
            RewardOffered = dto.RewardOffered?.Trim(),
            NumberOfHelpersNeeded = dto.NumberOfHelpersNeeded,
            DueAt = dto.DueAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.AssistanceRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Assistance request created. RequestId={RequestId} Kind={Kind} CreatedByProfileId={ProfileId}",
            entity.Id,
            entity.Kind,
            entity.CreatedByProfileId);

        await BroadcastAsync("AssistanceRequestCreated", entity.Id, ct);

        return Ok(new { requestId = entity.Id });
    }

    /// <summary>
    /// Claims an open assistance request, assigning the current user and setting status to Accepted.
    /// </summary>
    [HttpPatch("{id:guid}/claim")]
    public async Task<IActionResult> ClaimRequest(Guid id, CancellationToken ct)
    {
        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var request = await _db.AssistanceRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Assistance request not found." });

        if (request.Status != RequestStatus.Open)
            return BadRequest(new { error = "Only open requests can be claimed." });

        request.AssignedToProfileId = profile.Id;
        request.Status = RequestStatus.Accepted;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Assistance request claimed. RequestId={RequestId} AssignedToProfileId={ProfileId}",
            id,
            profile.Id);

        await BroadcastAsync("AssistanceRequestUpdated", id, ct);

        return Ok(new { message = "Request claimed.", requestId = id });
    }

    /// <summary>
    /// Marks an assistance request as completed.
    /// </summary>
    [HttpPatch("{id:guid}/complete")]
    public async Task<IActionResult> CompleteRequest(Guid id, CancellationToken ct)
    {
        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var request = await _db.AssistanceRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Assistance request not found." });

        if (request.Status == RequestStatus.Completed || request.Status == RequestStatus.Cancelled)
            return BadRequest(new { error = "Request is already in a terminal state." });

        request.Status = RequestStatus.Completed;
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Assistance request completed. RequestId={RequestId} ProfileId={ProfileId}",
            id,
            profile.Id);

        await BroadcastAsync("AssistanceRequestUpdated", id, ct);

        return Ok(new { message = "Request completed.", requestId = id });
    }

    /// <summary>
    /// Cancels an assistance request. Only the original creator may cancel.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelRequest(Guid id, CancellationToken ct)
    {
        var profile = await GetCurrentProfile(ct);
        if (profile is null)
            return Unauthorized();

        var request = await _db.AssistanceRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return NotFound(new { error = "Assistance request not found." });

        if (!CanManageRequest(profile, request.CreatedByProfileId))
            return StatusCode(403, new { error = "Only the creator may cancel this request." });

        if (request.Status == RequestStatus.Cancelled)
            return BadRequest(new { error = "Request is already cancelled." });

        request.Status = RequestStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Assistance request cancelled. RequestId={RequestId} ProfileId={ProfileId}",
            id,
            profile.Id);

        await BroadcastAsync("AssistanceRequestUpdated", id, ct);

        return Ok(new { message = "Request cancelled.", requestId = id });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Resolves the current authenticated user's profile from the Discord name identifier claim.
    /// </summary>
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
            _logger.LogWarning("No profile found for DiscordId={DiscordId}.", discordId);

        return profile;
    }

    /// <summary>
    /// Broadcasts an assistance request SignalR event to all connected clients.
    /// </summary>
    private async Task BroadcastAsync(string eventName, Guid id, CancellationToken ct)
    {
        _logger.LogDebug(
            "Broadcasting SignalR event. EventName={EventName} EntityId={EntityId}",
            eventName,
            id);

        await _hub.Clients.All.SendAsync(eventName, id, ct);
    }

    private bool CanManageRequest(Profile profile, Guid creatorProfileId) =>
        profile.Id == creatorProfileId || UserHasElevatedRequestRole(profile.DiscordRank);

    private bool UserHasElevatedRequestRole(string? profileRole) =>
        SecurityConstants.IsElevatedRequestRole(profileRole)
        || SecurityConstants.ElevatedRequestRoles.Any(User.IsInRole);

    #endregion
}
