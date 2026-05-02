using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAdminAssistanceRequestDetail;
using PackTracker.Application.Admin.Queries.GetAdminAssistanceRequestHistory;
using PackTracker.Application.Admin.Queries.GetAdminCraftingRequestDetail;
using PackTracker.Application.Admin.Queries.GetAdminCraftingRequestHistory;
using PackTracker.Application.Admin.Queries.GetAdminProcurementRequestDetail;
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

    [HttpGet("history/assistance/{id:guid}")]
    [ProducesResponseType(typeof(AdminRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssistanceDetail(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminAssistanceRequestDetailQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("history/crafting/{id:guid}")]
    [ProducesResponseType(typeof(AdminRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCraftingDetail(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminCraftingRequestDetailQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("history/procurement/{id:guid}")]
    [ProducesResponseType(typeof(AdminRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProcurementDetail(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminProcurementRequestDetailQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
