using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/guides")]
public class GuideRequestsController : ControllerBase
{
    private readonly AppDbContext _db;

    public GuideRequestsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("scheduled")]
    public async Task<IActionResult> GetScheduled()
    {
        var scheduled = await _db.GuideRequests
            .Where(g => g.Status == "Scheduled" || g.Status == "Assigned")
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();

        return Ok(scheduled);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrUpdate(GuideRequest dto)
    {
        var existing = await _db.GuideRequests.FirstOrDefaultAsync(x => x.ThreadId == dto.ThreadId);
        if (existing == null)
        {
            _db.GuideRequests.Add(dto);
        }
        else
        {
            existing.Status = dto.Status;
            existing.Title = dto.Title;
            existing.Requester = dto.Requester;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }
}
