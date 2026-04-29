using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAdminAccess;
using PackTracker.Application.Admin.Queries.GetAdminDashboardSummary;
using PackTracker.Domain.Security;

namespace PackTracker.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IMediator mediator, ILogger<DashboardController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("access")]
    [Authorize]
    [ProducesResponseType(typeof(AdminAccessDto), StatusCodes.Status200OK)]
    public async Task<AdminAccessDto> GetAccess(CancellationToken ct)
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameidentifier")
            ?? User.FindFirstValue("sub")
            ?? "unknown";

        _logger.LogInformation(
            "Admin access endpoint called. Authenticated={Authenticated}, UserId={UserId}, UserName={UserName}",
            User.Identity?.IsAuthenticated == true,
            userId,
            User.Identity?.Name ?? "<null>");

        var result = await _mediator.Send(new GetAdminAccessQuery(), ct);
        _logger.LogInformation(
            "Admin access endpoint result. UserId={UserId}, CanAccessAdmin={CanAccessAdmin}, HighestTier={HighestTier}",
            userId,
            result.CanAccessAdmin,
            result.HighestTier ?? "<null>");
        return result;
    }

    [HttpGet]
    [Authorize(Policy = AdminPolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(AdminDashboardSummaryDto), StatusCodes.Status200OK)]
    public Task<AdminDashboardSummaryDto> GetSummary(CancellationToken ct) =>
        _mediator.Send(new GetAdminDashboardSummaryQuery(), ct);
}
