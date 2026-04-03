using PackTracker.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

/// <summary name="RequestsController"> 
/// Controller for managing request tickets in the House Wolf community.
/// Requests can be created, updated, completed, and deleted.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "HouseWolfMember")]
public class RequestsController : ControllerBase
{
    #region Fields and Constructor
    private readonly AppDbContext _db;
    private readonly ILogger<RequestsController> _logger;
    private readonly IDiscordNotifier _notifier;
    [FromServices] public IHubContext<RequestsHub> _hub { get; set; } = null;

    public RequestsController(AppDbContext db, ILogger<RequestsController> logger, IDiscordNotifier notifier)
    {
        _db = db;
        _logger = logger;
        _notifier = notifier;
    }
    #endregion

    
    #region Endpoints
    /// <summary name="Query">
    /// Query request tickets with optional filters for status, kind, and ownership.
    /// <param name="status">
    /// Filter by request status (e.g., Open, Completed).
    /// </param>
    /// <param name="kind">
    /// Filter by request kind (e.g., Bug, Feature).
    /// </param>
    /// <param name="mine">
    /// If true, only return requests assigned to or created by the current user.
    /// </param>
    /// <param name="top">
    /// Maximum number of results to return (default 100, max 500).
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the request.
    /// </param>
    /// <returns>
    /// A list of request tickets matching the specified filters.
    /// Each ticket includes details such as title, description, kind, priority, status, and timestamps.
    /// The response also includes a success flag and the total count of returned tickets.
    /// </returns>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Query([FromQuery] RequestStatus? status, [FromQuery] RequestKind? kind,
        [FromQuery] bool? mine, [FromQuery] int top = 100, CancellationToken ct = default)
    {
        var q = _db.RequestTickets.AsNoTracking().OrderByDescending(r => r.CreatedAt).AsQueryable();
        
        if (status.HasValue) q = q.Where(r => r.Status == status);
        if (kind.HasValue) q = q.Where(r => r.Kind == kind);
        if (mine == true)
        {
            var me = User.Identity?.Name ?? "";
            q = q.Where(r => r.AssignedToDisplayName == me || r.CreatedByDisplayName == me);
        }

        var items = await q.Take(Math.Clamp(top, 1, 500)).ToListAsync(ct);


        var data = items.Select(Map).ToList();
        return Ok(new { success = true, count = data.Count, data });
    }

    /// <summary name="GetById">
    /// Get a specific request ticket by its ID.
    /// </summary>
    /// <param name="id">
    /// The ID of the request ticket to retrieve.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the request.
    /// </param>
    /// <returns>
    /// If found, returns the request ticket details including title, description, kind, priority, status, and timestamps.
    /// If not found, returns a 404 Not Found response.
    /// The response also includes a success flag.
    /// </returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var entity = await _db.RequestTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();
        return Ok(new { success = true, data = Map(entity) });
    }

    /// <summary name="Create">
    /// Create a new request ticket, setting the creator based on the authenticated user.
    /// The request will be initialized with an "Open" status.
    /// </summary>
    /// <param name="dto">
    /// The details of the request ticket to create, including title, description, kind, priority, and due date.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the request.
    /// </param>
    /// <returns>
    /// On success, returns a 201 Created response with the details of the newly created request ticket.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RequestCreateDto dto, CancellationToken ct)
    {
        var userId = User.FindFirst("sub")?.Value ?? "unknown";
        var display = User.Identity?.Name ?? "Unknown";

        var entity = new RequestTicket
        {
            Title = dto.Title,
            Description = dto.Description,
            Kind = dto.Kind,
            Priority = dto.Priority,
            DueAt = dto.DueAt,
            Status = RequestStatus.Open,
            CreatedByUserId = userId,
            CreatedByDisplayName = display,
            MaterialName = dto.MaterialName,
            QuantityNeeded = dto.QuantityNeeded,
            MeetingLocation = dto.MeetingLocation,
            RewardOffered = dto.RewardOffered,
            NumberOfHelpersNeeded = dto.NumberOfHelpersNeeded
        };
        
        _db.RequestTickets.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync("RequestUpdated", Map(entity), ct);


        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { success = true, data = Map(entity) });
    }

    /// <summary name="Update">
    /// Update an existing request ticket by its ID.
    /// Only certain fields can be updated, and the ticket must exist.
    /// </summary>
    /// <param name="id">
    /// The ID of the request ticket to update.
    /// </param>
    /// <param name="dto">
    /// The updated details for the request ticket, including title, description, kind, priority, status, assigned user, and due date.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the request.
    /// </param>
    /// <returns>
    /// If the ticket is found and updated, returns the updated ticket details.
    /// If not found, returns a 404 Not Found response.
    /// The response also includes a success flag.
    /// </returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] RequestUpdateDto dto, CancellationToken ct)
    {
        var entity = await _db.RequestTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Kind = dto.Kind;
        entity.Priority = dto.Priority;
        entity.Status = dto.Status;
        entity.AssignedToUserId = dto.AssignedToUserId;
        entity.AssignedToDisplayName = dto.AssignedToDisplayName;
        entity.DueAt = dto.DueAt;
        entity.MaterialName = dto.MaterialName;
        entity.QuantityNeeded = dto.QuantityNeeded;
        entity.MeetingLocation = dto.MeetingLocation;
        entity.RewardOffered = dto.RewardOffered;
        entity.NumberOfHelpersNeeded = dto.NumberOfHelpersNeeded;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync("RequestUpdated", Map(entity), ct);
        return Ok(new { success = true, data = Map(entity) });
    }

    /// <summary name="Claim">
    /// Claim a request ticket by assigning it to yourself.
    /// This sets the ticket's status to "InProgress" and assigns it to the current user.
    /// </summary>
    /// <param name="id">
    /// The ID of the request ticket to claim.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the request.
    /// </param>
    /// <returns>
    /// If the ticket is found and claimed, returns the updated ticket details.
    /// If not found, returns a 404 Not Found response.
    /// If already assigned, returns a 400 Bad Request response.
    /// The response also includes a success flag.
    /// </returns>
    [HttpPatch("{id:int}/claim")]
    public async Task<IActionResult> Claim([FromRoute] int id, CancellationToken ct)
    {
        var entity = await _db.RequestTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        if (!string.IsNullOrEmpty(entity.AssignedToUserId))
            return BadRequest(new { success = false, error = "Request is already claimed" });

        var userId = User.FindFirst("sub")?.Value ?? "unknown";
        var display = User.Identity?.Name ?? "Unknown";

        entity.Status = RequestStatus.InProgress;
        entity.AssignedToUserId = userId;
        entity.AssignedToDisplayName = display;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync("RequestUpdated", Map(entity), ct);
        return Ok(new { success = true, data = Map(entity) });
    }

    /// <summary name="Complete">
    /// Mark a specific request ticket as completed by its ID.
    /// This sets the ticket's status to "Completed" and records the completion timestamp and user
    /// based on the authenticated user.
    /// </summary>
    /// <param name="id">
    /// The ID of the request ticket to mark as completed.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the request.
    /// </param>
    /// <returns>
    /// If the ticket is found and marked as completed, returns the updated ticket details.
    /// If not found, returns a 404 Not Found response.
    /// The response also includes a success flag.
    /// </returns>
    [HttpPatch("{id:int}/complete")]
    public async Task<IActionResult> Complete([FromRoute] int id, CancellationToken ct)
    {
        var entity = await _db.RequestTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        entity.Status = RequestStatus.Completed;
        entity.CompletedAt = DateTime.UtcNow;
        entity.CompletedByUserId = User.FindFirst("sub")?.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync("RequestUpdated", Map(entity), ct);
        return Ok(new { success = true, data = Map(entity) });
    }

    /// <summary name="Delete">
    /// Delete a specific request ticket by its ID.
    /// </summary>
    /// <param name="id">
    /// The ID of the request ticket to delete.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the request.
    /// </param>
    /// <returns>
    /// If the ticket is found and deleted, returns a success flag.
    /// If not found, returns a 404 Not Found response.
    /// </returns> 
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        var entity = await _db.RequestTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        _db.RequestTickets.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
    #endregion


    #region Methods
    private static RequestDto Map(RequestTicket e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        Kind = e.Kind,
        Priority = e.Priority,
        Status = e.Status,
        CreatedByDisplayName = e.CreatedByDisplayName,
        AssignedToDisplayName = e.AssignedToDisplayName,
        DueAt = e.DueAt,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        CompletedAt = e.CompletedAt,
        MaterialName = e.MaterialName,
        QuantityNeeded = e.QuantityNeeded,
        MeetingLocation = e.MeetingLocation,
        RewardOffered = e.RewardOffered,
        NumberOfHelpersNeeded = e.NumberOfHelpersNeeded
    };
    #endregion
}
