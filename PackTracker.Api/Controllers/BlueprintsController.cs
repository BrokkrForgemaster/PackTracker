using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Blueprints.Queries.GetBlueprintById;
using PackTracker.Application.Blueprints.Queries.GetBlueprintCategories;
using PackTracker.Application.Blueprints.Queries.PingBlueprints;
using PackTracker.Application.Blueprints.Queries.SearchBlueprints;
using PackTracker.Application.DTOs.Crafting;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/[controller]")]
public class BlueprintsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BlueprintsController> _logger;

    public BlueprintsController(IMediator mediator, ILogger<BlueprintsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("ping")]
    [AllowAnonymous]
    public async Task<IActionResult> Ping(CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new PingBlueprintsQuery(), ct);
            return Ok(new
            {
                status = "ok",
                canConnect = result.CanConnect,
                provider = result.Provider,
                pendingMigrations = result.PendingMigrations,
                appliedMigrationsCount = result.AppliedMigrationsCount,
                timestamp = result.Timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection check failed during blueprint ping.");
            return StatusCode(500, new
            {
                status = "db_error",
                message = ex.Message,
                inner = ex.InnerException?.Message,
                type = ex.GetType().FullName
            });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BlueprintSearchItemDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] bool inGameOnly = true,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Blueprint search. QueryQ={QueryQ} Category={Category} InGameOnly={InGameOnly} User={User}",
            q, category, inGameOnly, User?.Identity?.Name ?? "<anonymous>");

        try
        {
            var items = await _mediator.Send(new SearchBlueprintsQuery(q, category, inGameOnly), ct);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint search query failed.");
            return StatusCode(500, new { message = "Blueprint search query failed.", detail = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlueprintDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Blueprint detail. BlueprintId={BlueprintId} User={User}", id, User?.Identity?.Name ?? "<anonymous>");

        try
        {
            var blueprint = await _mediator.Send(new GetBlueprintByIdQuery(id), ct);
            if (blueprint is null)
            {
                _logger.LogWarning("Blueprint {BlueprintId} not found.", id);
                return NotFound(new { message = "Blueprint not found.", id });
            }

            return Ok(blueprint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint detail failed for {BlueprintId}", id);
            return StatusCode(500, new { message = "Blueprint detail query failed.", detail = ex.Message });
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken ct)
    {
        try
        {
            var categories = await _mediator.Send(new GetBlueprintCategoriesQuery(), ct);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blueprint categories.");
            return StatusCode(500, new { message = "Failed to load categories.", detail = ex.Message });
        }
    }
}
