using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/guides")]
public class GuideRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<GuideRequestsController> _logger;

    public GuideRequestsController(AppDbContext db, ILogger<GuideRequestsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("scheduled")]
    public async Task<IActionResult> GetScheduled()
    {
        var scheduled = await _db.GuideRequests
            .Where(g => g.Status == "Scheduled" || g.Status == "Assigned")
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("Guide requests listed. Count={Count}", scheduled.Count);
        return Ok(scheduled);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrUpdate(GuideRequest dto)
    {
        var existing = await _db.GuideRequests.FirstOrDefaultAsync(x => x.ThreadId == dto.ThreadId);
        if (existing == null)
        {
            _db.GuideRequests.Add(dto);
            _logger.LogInformation(
                "Guide request created. ThreadId={ThreadId} Title={Title} Requester={Requester} Status={Status}",
                dto.ThreadId, dto.Title, dto.Requester, dto.Status);
        }
        else
        {
            _logger.LogInformation(
                "Guide request updated. ThreadId={ThreadId} Title={Title} PreviousStatus={Previous} NewStatus={New}",
                dto.ThreadId, dto.Title, existing.Status, dto.Status);
            existing.Status = dto.Status;
            existing.Title = dto.Title;
            existing.Requester = dto.Requester;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }
}
