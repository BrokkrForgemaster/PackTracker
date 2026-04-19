using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Requests.Assistance.CancelAssistanceRequest;
using PackTracker.Application.Requests.Assistance.ClaimAssistanceRequest;
using PackTracker.Application.Requests.Assistance.CompleteAssistanceRequest;
using PackTracker.Application.Requests.Assistance.CreateAssistanceRequest;
using PackTracker.Application.Requests.Assistance.PinAssistanceRequest;
using PackTracker.Application.Requests.Assistance.QueryAssistanceRequests;
using PackTracker.Domain.Enums;

namespace PackTracker.Api.Controllers;

/// <summary name="AssistanceRequestsController">
/// Handles general assistance request workflows for the Assistance Hub feature.
/// </summary>
[ApiController]
[Route("api/v1/requests")]
[Authorize]
public class AssistanceRequestsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<AssistanceRequestsController> _logger;

    public AssistanceRequestsController(
        ISender sender,
        ILogger<AssistanceRequestsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssistanceRequestDto>>> GetRequests(
        [FromQuery] RequestKind? kind,
        [FromQuery] RequestStatus? status,
        CancellationToken ct)
    {
        var items = await _sender.Send(new QueryAssistanceRequestsQuery(kind, status), ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest(
        [FromBody] RequestCreateDto dto,
        CancellationToken ct)
    {
        var result = await _sender.Send(new CreateAssistanceRequestCommand(dto), ct);
        if (!result.Success)
        {
            return ToFailureResult(result, "Request payload is required.");
        }

        _logger.LogInformation("Assistance request created. RequestId={RequestId}", result.Data);
        return Ok(new { requestId = result.Data });
    }

    [HttpPatch("{id:guid}/claim")]
    public async Task<IActionResult> ClaimRequest(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new ClaimAssistanceRequestCommand(id), ct);
        return ToMutationResult(result, "Request claimed.");
    }

    [HttpPatch("{id:guid}/complete")]
    public async Task<IActionResult> CompleteRequest(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new CompleteAssistanceRequestCommand(id), ct);
        return ToMutationResult(result, "Request completed.");
    }

    [HttpPost("{id}/pin")]
    [HttpPatch("{id:guid}/pin")]
    [HttpPatch("{id}/pin")]
    public async Task<IActionResult> PinRequest(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var requestId))
        {
            return BadRequest(new { error = "Invalid assistance request id." });
        }

        var result = await _sender.Send(new PinAssistanceRequestCommand(requestId, true), ct);
        return ToPinResult(result, requestId, true);
    }

    [HttpPost("{id}/unpin")]
    [HttpPatch("{id:guid}/unpin")]
    [HttpPatch("{id}/unpin")]
    public async Task<IActionResult> UnpinRequest(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var requestId))
        {
            return BadRequest(new { error = "Invalid assistance request id." });
        }

        var result = await _sender.Send(new PinAssistanceRequestCommand(requestId, false), ct);
        return ToPinResult(result, requestId, false);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelRequest(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new CancelAssistanceRequestCommand(id), ct);
        return ToMutationResult(result, "Request cancelled.");
    }

    private IActionResult ToMutationResult(OperationResult<Guid> result, string successMessage)
    {
        if (result.Success)
        {
            return Ok(new { message = successMessage, requestId = result.Data });
        }

        return ToFailureResult(result, successMessage);
    }

    private IActionResult ToPinResult(OperationResult<Guid> result, Guid requestId, bool isPinned)
    {
        if (result.Success)
        {
            return Ok(new
            {
                message = isPinned ? "Request pinned." : "Request unpinned.",
                requestId
            });
        }

        if (string.Equals(result.Message, "Only Captains and above may manage pins.", StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = result.Message });
        }

        return ToFailureResult(result, isPinned ? "Request pinned." : "Request unpinned.");
    }

    private IActionResult ToFailureResult(OperationResult<Guid> result, string fallbackMessage)
    {
        if (string.Equals(result.Message, "Unauthorized", StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        if (string.Equals(result.Message, "Assistance request not found.", StringComparison.Ordinal))
        {
            return NotFound(new { error = result.Message });
        }

        if (string.Equals(result.Message, "Only the creator may cancel this request.", StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = result.Message });
        }

        return BadRequest(new { error = result.Message ?? fallbackMessage });
    }
}
