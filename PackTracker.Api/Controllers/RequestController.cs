using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;
using PackTracker.Application.Requests.Legacy.ClaimLegacyRequest;
using PackTracker.Application.Requests.Legacy.CreateLegacyRequest;
using PackTracker.Application.Requests.Legacy.QueryLegacyRequests;
using PackTracker.Application.Requests.Legacy.UpdateLegacyRequest;

namespace PackTracker.Api.Controllers;

/// <summary>
/// Handles general request ticket operations (legacy/general request system).
/// </summary>
[ApiController]
[Route("api/v1/legacy/requests")]
[Authorize(Roles = SecurityConstants.Roles.HouseWolfMember)]
public class RequestsController : ControllerBase
{
    #region Fields

    private readonly ISender _sender;
    private readonly ILogger<RequestsController> _logger;

    #endregion

    #region Constructor

    public RequestsController(
        ISender sender,
        ILogger<RequestsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    #endregion

    #region Query

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] RequestStatus? status,
        [FromQuery] RequestKind? kind,
        [FromQuery] bool? mine,
        [FromQuery] int top = 100,
        CancellationToken ct = default)
    {
        var items = await _sender.Send(new QueryLegacyRequestsQuery(status, kind, mine, top), ct);

        _logger.LogInformation("Requests queried. Count={Count}", items.Count);

        return Ok(new
        {
            success = true,
            count = items.Count,
            data = items
        });
    }

    #endregion

    #region Create

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] RequestCreateDto dto,
        CancellationToken ct)
    {
        var result = await _sender.Send(new CreateLegacyRequestCommand(dto), ct);
        return ToActionResult(result);
    }

    #endregion

    #region Update

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] RequestUpdateDto dto, CancellationToken ct)
    {
        var result = await _sender.Send(new UpdateLegacyRequestCommand(id, dto), ct);
        return ToActionResult(result);
    }

    #endregion

    #region Actions

    [HttpPatch("{id:int}/claim")]
    public async Task<IActionResult> Claim(int id, CancellationToken ct)
    {
        var result = await _sender.Send(new ClaimLegacyRequestCommand(id), ct);
        if (!result.Success && string.Equals(result.Message, "Already claimed", StringComparison.Ordinal))
        {
            return BadRequest(result.Message);
        }

        return ToActionResult(result);
    }

    #endregion

    #region Helpers

    private ActionResult ToActionResult(OperationResult<RequestTicketDto> result)
    {
        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        if (string.Equals(result.Message, "Request ticket was not found.", StringComparison.Ordinal))
        {
            return NotFound();
        }

        return BadRequest(result.Message);
    }
    #endregion
}
