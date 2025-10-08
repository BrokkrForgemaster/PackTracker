using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOS.Request;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "HouseWolfMember")]
public class RequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(AppDbContext db, ILogger<RequestsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET /api/v1/requests?status=&kind=&mine=&top=
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

    // GET /api/v1/requests/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var entity = await _db.RequestTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();
        return Ok(new { success = true, data = Map(entity) });
    }

    // POST /api/v1/requests
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
        };

        _db.RequestTickets.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { success = true, data = Map(entity) });
    }

    // PUT /api/v1/requests/{id}
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
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = Map(entity) });
    }

    // PATCH /api/v1/requests/{id}/complete
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
        return Ok(new { success = true, data = Map(entity) });
    }

    // DELETE /api/v1/requests/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        var entity = await _db.RequestTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        _db.RequestTickets.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

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
        CompletedAt = e.CompletedAt
    };
}
