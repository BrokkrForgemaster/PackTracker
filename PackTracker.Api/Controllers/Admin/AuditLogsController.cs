using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAuditLogs;

namespace PackTracker.Api.Controllers.Admin;

/// <summary name="AuditLogsController">
/// Controller for retrieving audit logs. This is intended for admin use only,
/// and should be protected by appropriate authorization policies.
/// </summary>
public sealed class AuditLogsController : AdminControllerBase
{
    #region Properties
    private readonly IMediator _mediator;
    #endregion

    #region Constructors
    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    #endregion

    #region Endpoints
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminAuditLogListItemDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AdminAuditLogListItemDto>> Get([FromQuery] int take = 100, CancellationToken ct = default) =>
        _mediator.Send(new GetAuditLogsQuery(take), ct);
    #endregion
}
