using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.DTOs.Wiki;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/wiki")]
public class WikiSyncController : ControllerBase
{
    private readonly IWikiSyncService _wikiSync;
    private readonly ILogger<WikiSyncController> _logger;

    // In-memory last sync timestamps (reset on restart — acceptable for this use case)
    private static DateTime? _lastBlueprintSync;
    private static DateTime? _lastItemSync;

    public WikiSyncController(IWikiSyncService wikiSync, ILogger<WikiSyncController> logger)
    {
        _wikiSync = wikiSync;
        _logger = logger;
    }

    /// <summary>Triggers a full blueprint sync from the Star Citizen Wiki API.</summary>
    [HttpPost("sync/blueprints")]
    public async Task<ActionResult<WikiSyncResult>> SyncBlueprints(CancellationToken ct)
    {
        _logger.LogInformation("Blueprint sync triggered via API by {User}", User.Identity?.Name ?? "unknown");
        var result = await _wikiSync.SyncBlueprintsAsync(ct);
        _lastBlueprintSync = DateTime.UtcNow;
        return Ok(result);
    }

    /// <summary>Triggers a full item sync (mining, armor, weapons) from the Star Citizen Wiki API.</summary>
    [HttpPost("sync/items")]
    public async Task<ActionResult<WikiSyncResult>> SyncItems(CancellationToken ct)
    {
        _logger.LogInformation("Items sync triggered via API by {User}", User.Identity?.Name ?? "unknown");
        var result = await _wikiSync.SyncItemsAsync(ct);
        _lastItemSync = DateTime.UtcNow;
        return Ok(result);
    }

    /// <summary>Returns the last recorded sync timestamps.</summary>
    [HttpGet("sync/status")]
    public ActionResult<WikiSyncStatusDto> GetStatus()
    {
        return Ok(new WikiSyncStatusDto
        {
            LastBlueprintSync = _lastBlueprintSync?.ToString("O"),
            LastItemSync = _lastItemSync?.ToString("O")
        });
    }
}

public class WikiSyncStatusDto
{
    public string? LastBlueprintSync { get; set; }
    public string? LastItemSync { get; set; }
}
