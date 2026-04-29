using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAuditLogs;

namespace PackTracker.Api.Controllers.Admin;

public sealed class AuditLogsController : AdminControllerBase
{
    private readonly IMediator _mediator;

    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminAuditLogListItemDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AdminAuditLogListItemDto>> Get([FromQuery] int take = 100, CancellationToken ct = default) =>
        _mediator.Send(new GetAuditLogsQuery(take), ct);
}
