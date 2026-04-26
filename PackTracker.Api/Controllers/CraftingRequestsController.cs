using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Common;
using PackTracker.Application.Crafting.Commands.AddCraftingComment;
using PackTracker.Application.Crafting.Commands.AddProcurementComment;
using PackTracker.Application.Crafting.Commands.AssignCraftingRequest;
using PackTracker.Application.Crafting.Commands.ClaimProcurementRequest;
using PackTracker.Application.Crafting.Commands.CreateCraftingRequest;
using PackTracker.Application.Crafting.Commands.CreateProcurementRequest;
using PackTracker.Application.Crafting.Commands.DeleteCraftingRequest;
using PackTracker.Application.Crafting.Commands.DeleteProcurementRequest;
using PackTracker.Application.Crafting.Commands.RefuseCraftingRequest;
using PackTracker.Application.Crafting.Commands.RefuseProcurementRequest;
using PackTracker.Application.Crafting.Commands.UpdateCraftingRequestStatus;
using PackTracker.Application.Crafting.Commands.UpdateProcurementStatus;
using PackTracker.Application.Crafting.Queries.GetCraftingRequestComments;
using PackTracker.Application.Crafting.Queries.GetCraftingRequests;
using PackTracker.Application.Crafting.Queries.GetProcurementRequestComments;
using PackTracker.Application.Crafting.Queries.GetProcurementRequests;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.DTOs.Request;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/crafting")]
[Authorize]
public class CraftingRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CraftingRequestsController> _logger;

    public CraftingRequestsController(IMediator mediator, ILogger<CraftingRequestsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    #region Crafting Requests

    [HttpPost("requests")]
    public async Task<IActionResult> CreateCraftingRequest(
        [FromBody] CreateCraftingRequestDto request,
        CancellationToken ct)
    {
        if (request is null)
        {
            _logger.LogWarning("CreateCraftingRequest received null payload.");
            return BadRequest("Request payload is required.");
        }

        var result = await _mediator.Send(new CreateCraftingRequestCommand(request), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Blueprint not found.");

        return Ok(new { message = "Crafting request created.", requestId = result.Data });
    }

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<CraftingRequestListItemDto>>> GetCraftingRequests(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetCraftingRequestsQuery(), ct));
    }

    [HttpGet("requests/{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<RequestCommentDto>>> GetComments(Guid id, CancellationToken ct)
    {
        var comments = await _mediator.Send(new GetCraftingRequestCommentsQuery(id, false), ct);
        if (comments is null)
            return NotFound(new { error = "Crafting request not found." });

        return Ok(comments);
    }

    [HttpPost("requests/{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddRequestCommentDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "Comment content is required." });

        var result = await _mediator.Send(new AddCraftingCommentCommand(id, dto), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Crafting request not found.");
        return Ok(new { message = "Comment added.", commentId = result.Data });
    }

    [HttpGet("requests/{id:guid}/live-chat")]
    public async Task<ActionResult<IReadOnlyList<RequestCommentDto>>> GetLiveChat(Guid id, CancellationToken ct)
    {
        var chat = await _mediator.Send(new GetCraftingRequestCommentsQuery(id, true), ct);
        if (chat is null)
            return NotFound(new { error = "Crafting request not found." });

        return Ok(chat);
    }

    [HttpPatch("requests/{id:guid}/assign")]
    public async Task<IActionResult> AssignToSelf(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new AssignCraftingRequestCommand(id), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Crafting request not found.");
        return Ok(new { message = "Request assigned.", requestId = result.Data });
    }

    [HttpPatch("requests/{id:guid}/refuse")]
    public async Task<IActionResult> RefuseRequest(Guid id, [FromBody] RefuseRequestDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { error = "Refusal reason is required." });

        var result = await _mediator.Send(new RefuseCraftingRequestCommand(id, dto), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Crafting request not found.");
        return Ok(new { message = "Request refused.", requestId = result.Data });
    }

    [HttpPatch("requests/{id:guid}/status")]
    public async Task<IActionResult> UpdateCraftingRequestStatus(Guid id, [FromBody] UpdateRequestStatusDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Status))
            return BadRequest(new { error = "Status is required." });

        var result = await _mediator.Send(new UpdateCraftingRequestStatusCommand(id, dto.Status), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Crafting request not found.");
        return Ok(new { message = result.Message, requestId = result.RequestId, previousStatus = result.PreviousStatus, newStatus = result.NewStatus });
    }

    [HttpDelete("requests/{id:guid}")]
    public async Task<IActionResult> DeleteCraftingRequest(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCraftingRequestCommand(id), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Crafting request not found.");
        return Ok(new { message = "Crafting request removed.", requestId = result.Data });
    }

    #endregion

    #region Procurement Requests

    [HttpGet("procurement-requests")]
    public async Task<ActionResult<IReadOnlyList<MaterialProcurementRequestListItemDto>>> GetProcurementRequests(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetProcurementRequestsQuery(), ct));
    }

    [HttpGet("procurement-requests/{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<RequestCommentDto>>> GetProcurementComments(Guid id, CancellationToken ct)
    {
        var comments = await _mediator.Send(new GetProcurementRequestCommentsQuery(id), ct);
        if (comments is null)
            return NotFound(new { error = "Procurement request not found." });

        return Ok(comments);
    }

    [HttpPost("procurement-requests/{id:guid}/comments")]
    public async Task<IActionResult> AddProcurementComment(Guid id, [FromBody] AddRequestCommentDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "Comment content is required." });

        var result = await _mediator.Send(new AddProcurementCommentCommand(id, dto), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Procurement request not found.");
        return Ok(new { message = "Comment added.", commentId = result.Data });
    }

    [HttpPatch("procurement-requests/{id:guid}/claim")]
    public async Task<IActionResult> ClaimProcurementRequest(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ClaimProcurementRequestCommand(id), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Procurement request not found.");
        return Ok(new { message = "Request claimed.", requestId = result.Data });
    }

    [HttpPatch("procurement-requests/{id:guid}/refuse")]
    public async Task<IActionResult> RefuseProcurementRequest(Guid id, [FromBody] RefuseRequestDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { error = "Refusal reason is required." });

        var result = await _mediator.Send(new RefuseProcurementRequestCommand(id, dto), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Procurement request not found.");
        return Ok(new { message = "Request refused.", requestId = result.Data });
    }

    [HttpPost("procurement-requests")]
    public async Task<IActionResult> CreateProcurementRequest(
        [FromBody] CreateMaterialProcurementRequestDto request,
        CancellationToken ct)
    {
        if (request is null)
        {
            _logger.LogWarning("CreateProcurementRequest received null payload.");
            return BadRequest("Request payload is required.");
        }

        var result = await _mediator.Send(new CreateProcurementRequestCommand(request), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Linked crafting request not found.");

        return Ok(new { message = "Procurement request created.", requestId = result.Data });
    }

    #endregion

    #region Status Updates

    [HttpPatch("procurement-requests/{id:guid}/status")]
    public async Task<IActionResult> UpdateProcurementStatus(
        Guid id,
        [FromBody] UpdateRequestStatusDto dto,
        CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Status))
        {
            _logger.LogWarning("UpdateProcurementStatus received invalid payload for RequestId={RequestId}.", id);
            return BadRequest(new { error = "Status is required." });
        }

        var result = await _mediator.Send(new UpdateProcurementStatusCommand(id, dto.Status), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Procurement request not found.");
        return Ok(new { message = result.Message, requestId = result.RequestId, previousStatus = result.PreviousStatus, newStatus = result.NewStatus });
    }

    [HttpDelete("procurement-requests/{id:guid}")]
    public async Task<IActionResult> DeleteProcurementRequest(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteProcurementRequestCommand(id), ct);
        if (!result.Success)
            return ToErrorResult(result.Message, "Procurement request not found.");
        return Ok(new { message = "Procurement request removed.", requestId = result.Data });
    }

    #endregion

    #region Helpers

    private IActionResult ToErrorResult(string? message, string fallbackNotFoundMessage)
    {
        if (string.Equals(message, "Unauthorized", StringComparison.Ordinal))
            return Unauthorized();

        if (string.Equals(message, fallbackNotFoundMessage, StringComparison.Ordinal)
            || string.Equals(message, "Crafting request not found.", StringComparison.Ordinal)
            || string.Equals(message, "Procurement request not found.", StringComparison.Ordinal))
            return NotFound(new { error = message });

        if (string.Equals(message, "Only the creator or authorized leadership may cancel this request.", StringComparison.Ordinal)
            || string.Equals(message, "Only the creator or authorized leadership may remove this request.", StringComparison.Ordinal)
            || string.Equals(message, "Only the creator may complete this request.", StringComparison.Ordinal))
            return StatusCode(403, new { error = message });

        return BadRequest(new { error = message });
    }

    #endregion
}
