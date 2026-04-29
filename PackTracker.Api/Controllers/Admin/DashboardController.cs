using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAdminAccess;
using PackTracker.Application.Admin.Queries.GetAdminDashboardSummary;

namespace PackTracker.Api.Controllers.Admin;

public sealed class DashboardController : AdminControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("access")]
    [ProducesResponseType(typeof(AdminAccessDto), StatusCodes.Status200OK)]
    public Task<AdminAccessDto> GetAccess(CancellationToken ct) =>
        _mediator.Send(new GetAdminAccessQuery(), ct);

    [HttpGet]
    [ProducesResponseType(typeof(AdminDashboardSummaryDto), StatusCodes.Status200OK)]
    public Task<AdminDashboardSummaryDto> GetSummary(CancellationToken ct) =>
        _mediator.Send(new GetAdminDashboardSummaryQuery(), ct);
}
