using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Blueprints.Queries.GetBlueprintById;
using PackTracker.Application.Blueprints.Queries.GetBlueprintCategories;
using PackTracker.Application.Blueprints.Queries.GetOwnedBlueprints;
using PackTracker.Application.Blueprints.Queries.PingBlueprints;
using PackTracker.Application.Blueprints.Queries.SearchBlueprints;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Blueprints.Queries.GetBlueprintEconomicSummary;

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
                status = result.DiagnosticsErrorMessage is null ? "ok" : "degraded",
                canConnect = result.CanConnect,
                provider = result.Provider,
                pendingMigrations = result.PendingMigrations,
                appliedMigrationsCount = result.AppliedMigrationsCount,
                diagnosticsErrorMessage = result.DiagnosticsErrorMessage,
                startupInitialized = result.IsStartupInitialized,
                startupFailureMessage = result.StartupFailureMessage,
                startupCompletedAtUtc = result.StartupCompletedAtUtc,
                timestamp = result.Timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection check failed during blueprint ping.");
            return ServerError("Database connection check failed.");
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
            return ServerError("Blueprint search query failed.");
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
            return ServerError("Blueprint detail query failed.");
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
            return ServerError("Failed to load categories.");
        }
    }

    [HttpGet("owned")]
    public async Task<ActionResult<IReadOnlyList<OwnedBlueprintSummaryDto>>> GetOwned(CancellationToken ct)
    {
        try
        {
            var items = await _mediator.Send(new GetOwnedBlueprintsQuery(), ct);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load current user's owned blueprints.");
            return ServerError("Failed to load owned blueprints.");
        }
    }

    [HttpGet("{id:guid}/economic-summary")]
    public async Task<ActionResult<BlueprintEconomicSummaryDto>> GetEconomicSummary(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBlueprintEconomicSummaryQuery(id), ct);
        if (!result.Success) return NotFound(new { message = result.Message });
        return Ok(result.Data);
    }

    private ObjectResult ServerError(string message) =>
        StatusCode(500, new
        {
            message,
            traceId = HttpContext.TraceIdentifier
        });
}
