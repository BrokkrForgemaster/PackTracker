using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Guides.Commands.UpsertGuideRequest;
using PackTracker.Application.Guides.Queries.GetScheduledGuideRequests;
using PackTracker.Domain.Entities;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/guides")]
public class GuideRequestsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<GuideRequestsController> _logger;

    public GuideRequestsController(ISender sender, ILogger<GuideRequestsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet("scheduled")]
    public async Task<IActionResult> GetScheduled(CancellationToken ct)
    {
        var scheduled = await _sender.Send(new GetScheduledGuideRequestsQuery(), ct);

        _logger.LogInformation("Guide requests listed. Count={Count}", scheduled.Count);
        return Ok(scheduled);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrUpdate(GuideRequest dto, CancellationToken ct)
    {
        await _sender.Send(new UpsertGuideRequestCommand(dto), ct);

        _logger.LogInformation(
            "Guide request upserted. ThreadId={ThreadId} Title={Title} Requester={Requester} Status={Status}",
            dto.ThreadId,
            dto.Title,
            dto.Requester,
            dto.Status);

        return Ok();
    }
}
