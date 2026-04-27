using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Blueprints.Commands.RegisterBlueprintOwnership;
using PackTracker.Application.Blueprints.Commands.RemoveBlueprintOwnership;
using PackTracker.Application.Blueprints.Queries.GetBlueprintOwners;
using PackTracker.Application.DTOs.Crafting;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/blueprints/{blueprintId:guid}/ownership")]
public class BlueprintOwnershipController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BlueprintOwnershipController> _logger;

    public BlueprintOwnershipController(IMediator mediator, ILogger<BlueprintOwnershipController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        Guid blueprintId,
        [FromBody] RegisterBlueprintOwnershipRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        _logger.LogInformation(
            "Ownership register. BlueprintId={BlueprintId} InterestType={InterestType}",
            blueprintId, request.InterestType);

        var result = await _mediator.Send(new RegisterBlueprintOwnershipCommand(blueprintId, request), ct);

        return result.Status switch
        {
            BlueprintOwnershipRegistrationStatus.Success => Ok(new
            {
                message = result.Message,
                ownershipId = result.OwnershipId,
                ownerCount = result.OwnerCount
            }),
            BlueprintOwnershipRegistrationStatus.BlueprintNotFound => NotFound(new
            {
                message = result.Message,
                blueprintId
            }),
            _ => Unauthorized(new
            {
                message = result.Message
            })
        };
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(Guid blueprintId, CancellationToken ct)
    {
        _logger.LogInformation("Ownership remove. BlueprintId={BlueprintId}", blueprintId);

        var result = await _mediator.Send(new RemoveBlueprintOwnershipCommand(blueprintId), ct);

        if (!result.Success)
        {
            if (result.Message == "Unauthorized") return Unauthorized();
            if (result.Message == "Blueprint ownership not found.") return NotFound(new { message = result.Message });
            return BadRequest(new { message = result.Message });
        }

        return Ok(new
        {
            message = result.Message,
            ownerCount = result.OwnerCount
        });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BlueprintOwnerDto>>> GetOwners(Guid blueprintId, CancellationToken ct)
    {
        _logger.LogInformation("Ownership GET. BlueprintId={BlueprintId}", blueprintId);

        var owners = await _mediator.Send(new GetBlueprintOwnersQuery(blueprintId), ct);
        if (owners is null)
        {
            _logger.LogWarning("Ownership GET failed. BlueprintId={BlueprintId} not found.", blueprintId);
            return NotFound(new { message = "Blueprint not found.", blueprintId });
        }

        return Ok(owners);
    }
}
