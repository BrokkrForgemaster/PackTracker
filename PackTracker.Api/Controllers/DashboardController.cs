using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Dashboard.Queries.GetDashboardSummary;
using PackTracker.Application.DTOs.Dashboard;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ISender sender, ILogger<DashboardController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
    {
        var summary = await _sender.Send(new GetDashboardSummaryQuery(), ct);

        _logger.LogInformation(
            "Dashboard summary requested. ActiveRequests={ActiveRequests} ScheduledGuides={ScheduledGuides}",
            summary?.ActiveRequests.Count ?? 0,
            summary?.ScheduledGuides.Count ?? 0);

        return Ok(summary);
    }
}
