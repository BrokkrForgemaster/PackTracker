using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "HouseWolfMember")]
public class UexController : ControllerBase
{
    private readonly IUexService _uex;
    private readonly ILogger<UexController> _logger;

    public UexController(IUexService uex, ILogger<UexController> logger)
    {
        _uex = uex;
        _logger = logger;
    }

    // 📦 GET ALL COMMODITIES
    [HttpGet("commodities")]
    [AllowAnonymous] // optional: make public if you want basic data accessible
    public async Task<IActionResult> GetCommodities(CancellationToken ct)
    {
        var traceId = Guid.NewGuid().ToString();
        _logger.LogInformation("📦 [{Trace}] Retrieving all commodities...", traceId);

        try
        {
            var data = await _uex.CommoditiesAsync(ct);
            return Ok(new { traceId, success = true, count = data.Count, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [{Trace}] Error retrieving commodities.", traceId);
            return StatusCode(500, new { traceId, success = false, error = ex.Message });
        }
    }

    // 💰 GET PRICES BY CODE
    [HttpGet("prices/by-code")]
    public async Task<IActionResult> GetPricesByCode([FromQuery] string commodityCode, CancellationToken ct)
    {
        var traceId = Guid.NewGuid().ToString();
        _logger.LogInformation("💰 [{Trace}] Retrieving prices for commodity code {Code}.", traceId, commodityCode);

        try
        {
            if (string.IsNullOrWhiteSpace(commodityCode))
                return BadRequest(new { traceId, success = false, error = "Commodity code is required." });

            var data = await _uex.GetCommodityPricesAsync(commodityCode, ct);
            return Ok(new { traceId, success = true, count = data.Count, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [{Trace}] Error retrieving prices for {Code}.", traceId, commodityCode);
            return StatusCode(500, new { traceId, success = false, error = ex.Message });
        }
    }

    // 🚀 GET ROUTES BY CODE
    [HttpGet("routes/by-code")]
    public async Task<IActionResult> GetRoutesByCode([FromQuery] string commodityCode, [FromQuery] int limit = 25, CancellationToken ct = default)
    {
        var traceId = Guid.NewGuid().ToString();
        _logger.LogInformation("🚀 [{Trace}] Retrieving routes for commodity code {Code} (limit {Limit}).", traceId, commodityCode, limit);

        try
        {
            if (string.IsNullOrWhiteSpace(commodityCode))
                return BadRequest(new { traceId, success = false, error = "Commodity code is required." });

            var routes = await _uex.GetRoutesByCommodityCodeAsync(commodityCode, limit, ct);
            return Ok(new { traceId, success = true, count = routes.Count, data = routes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [{Trace}] Error retrieving routes for {Code}.", traceId, commodityCode);
            return StatusCode(500, new { traceId, success = false, error = ex.Message });
        }
    }

    // 🧭 (Optional Legacy) GET ROUTES BY LOCAL DB ID
    [HttpGet("routes/by-id")]
    public async Task<IActionResult> GetRoutesById([FromQuery] int commodityId, [FromQuery] int limit = 25, CancellationToken ct = default)
    {
        var traceId = Guid.NewGuid().ToString();
        _logger.LogInformation("🧭 [{Trace}] Retrieving routes for local commodity id {Id}.", traceId, commodityId);

        try
        {
            if (commodityId <= 0)
                return BadRequest(new { traceId, success = false, error = "Invalid commodity id." });

            var routes = await _uex.GetRoutesByCommodityAsync(commodityId, limit, ct);
            return Ok(new { traceId, success = true, count = routes.Count, data = routes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [{Trace}] Error retrieving routes for id {Id}.", traceId, commodityId);
            return StatusCode(500, new { traceId, success = false, error = ex.Message });
        }
    }
}
