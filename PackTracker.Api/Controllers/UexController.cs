using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UexController : ControllerBase
{
    private readonly IUexService _uexService;
    private readonly ILogger<UexController> _logger;

    public UexController(IUexService uexService, ILogger<UexController> logger)
    {
        _uexService = uexService;
        _logger = logger;
    }

    /// <summary>
    /// Syncs commodities from UEX API into the local database.
    /// </summary>
    [HttpPost("commodities")]
    [Authorize(Roles = "HouseWolfMember")] // ✅ optional: only allow org members
    public async Task<IActionResult> SyncCommodities(CancellationToken ct)
    {
        _logger.LogInformation("Manual sync: UEX commodities requested.");
        await _uexService.SyncCommoditiesAsync(ct);
        return Ok(new { message = "UEX commodities synced successfully." });
    }

    /// <summary>
    /// Syncs commodity prices from UEX API into the local database.
    /// </summary>
    [HttpPost("prices")]
    [Authorize(Roles = "HouseWolfMember")]
    public async Task<IActionResult> SyncPrices(CancellationToken ct)
    {
        _logger.LogInformation("Manual sync: UEX prices requested.");
        await _uexService.SyncCommodityPricesAsync(ct);
        return Ok(new { message = "UEX commodity prices synced successfully." });
    }
    
    [HttpGet("routes/top")]
    [Authorize(Roles = "HouseWolfMember")]
    public async Task<IActionResult> GetTopRoutes(
        [FromQuery] int originTerminalId,
        [FromQuery] int? destinationTerminalId,
        [FromQuery] int top = 5,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Manual request: Top {Top} routes from UEX. Origin={Origin}, Destination={Destination}", 
            top, originTerminalId, destinationTerminalId);

        var routes = await _uexService.GetTopRoutesAsync(originTerminalId, destinationTerminalId, top, ct);
        return Ok(routes);
    }
}