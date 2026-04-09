using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Api.Hubs;
using RequestsHub = PackTracker.Api.Hubs.RequestsHub;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

/// <summary>
/// Handles general request ticket operations (legacy/general request system).
/// </summary>
[ApiController]
[Route("api/v1/requests")]
[Authorize(Roles = "HouseWolfMember")]
public class RequestsController : ControllerBase
{
    #region Fields

    private readonly AppDbContext _db;
    private readonly ILogger<RequestsController> _logger;
    private readonly IDiscordNotifier _discord;
    private readonly IHubContext<RequestsHub> _hub;

    #endregion

    #region Constructor

    public RequestsController(
        AppDbContext db,
        ILogger<RequestsController> logger,
        IDiscordNotifier discord,
        IHubContext<RequestsHub> hub)
    {
        _db = db;
        _logger = logger;
        _discord = discord;
        _hub = hub;
    }

    #endregion

    #region Query

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] RequestStatus? status,
        [FromQuery] RequestKind? kind,
        [FromQuery] bool? mine,
        [FromQuery] int top = 100,
        CancellationToken ct = default)
    {
        var query = _db.RequestTickets.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status);

        if (kind.HasValue)
            query = query.Where(x => x.Kind == kind);

        if (mine == true)
        {
            var me = User.Identity?.Name ?? "";
            query = query.Where(x =>
                x.AssignedToDisplayName == me ||
                x.CreatedByDisplayName == me);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(top, 1, 500))
            .ToListAsync(ct);

        _logger.LogInformation("Requests queried. Count={Count}", items.Count);

        return Ok(new
        {
            success = true,
            count = items.Count,
            data = items.Select(Map).ToList()
        });
    }

    #endregion

    #region Create

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] RequestCreateDto dto,
        CancellationToken ct)
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

        await BroadcastUpdate(entity, ct);

        _logger.LogInformation("Request created. Id={Id} Title={Title}", entity.Id, entity.Title);

        return Ok(Map(entity));
    }

    #endregion

    #region Update

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] RequestUpdateDto dto, CancellationToken ct)
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
        await BroadcastUpdate(entity, ct);

        return Ok(Map(entity));
    }

    #endregion

    #region Actions

    [HttpPatch("{id:int}/claim")]
    public async Task<IActionResult> Claim(int id, CancellationToken ct)
    {
        var entity = await _db.RequestTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        if (!string.IsNullOrEmpty(entity.AssignedToUserId))
            return BadRequest("Already claimed");

        entity.Status = RequestStatus.InProgress;
        entity.AssignedToUserId = User.FindFirst("sub")?.Value;
        entity.AssignedToDisplayName = User.Identity?.Name;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await BroadcastUpdate(entity, ct);

        return Ok(Map(entity));
    }

    #endregion

    #region Helpers

    private async Task BroadcastUpdate(RequestTicket entity, CancellationToken ct)
    {
        await _hub.Clients.All.SendAsync("RequestUpdated", Map(entity), ct);
    }

    private static RequestTicketDto Map(RequestTicket e) => new()
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