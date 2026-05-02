using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAdminAssistanceRequestHistory;
using PackTracker.Application.Admin.Queries.GetAdminCraftingRequestHistory;
using PackTracker.Application.Admin.Queries.GetAdminProcurementRequestHistory;

namespace PackTracker.Api.Controllers.Admin;

public sealed class RequestsController : AdminControllerBase
{
    private readonly IMediator _mediator;

    public RequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("history/assistance")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminRequestHistoryItemDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AdminRequestHistoryItemDto>> GetAssistanceHistory(CancellationToken ct = default) =>
        _mediator.Send(new GetAdminAssistanceRequestHistoryQuery(), ct);

    [HttpGet("history/crafting")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminRequestHistoryItemDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AdminRequestHistoryItemDto>> GetCraftingHistory(CancellationToken ct = default) =>
        _mediator.Send(new GetAdminCraftingRequestHistoryQuery(), ct);

    [HttpGet("history/procurement")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminRequestHistoryItemDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AdminRequestHistoryItemDto>> GetProcurementHistory(CancellationToken ct = default) =>
        _mediator.Send(new GetAdminProcurementRequestHistoryQuery(), ct);
}
