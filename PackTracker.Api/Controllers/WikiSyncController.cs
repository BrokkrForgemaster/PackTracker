using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Wiki;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/wiki")]
public class WikiSyncController : ControllerBase
{
    private readonly IWikiSyncService _wikiSync;
    private readonly AppDbContext _db;
    private readonly ILogger<WikiSyncController> _logger;

    public WikiSyncController(IWikiSyncService wikiSync, AppDbContext db, ILogger<WikiSyncController> logger)
    {
        _wikiSync = wikiSync;
        _db = db;
        _logger = logger;
    }

    /// <summary>Triggers a full blueprint sync from the Star Citizen Wiki API.</summary>
    [HttpPost("sync/blueprints")]
    public async Task<ActionResult<WikiSyncResult>> SyncBlueprints(CancellationToken ct)
    {
        _logger.LogInformation("Blueprint sync triggered via API by {User}", User.Identity?.Name ?? "unknown");
        var result = await _wikiSync.SyncBlueprintsAsync(ct);
        return Ok(result);
    }

    /// <summary>Triggers a full item sync (mining, armor, weapons) from the Star Citizen Wiki API.</summary>
    [HttpPost("sync/items")]
    public async Task<ActionResult<WikiSyncResult>> SyncItems(CancellationToken ct)
    {
        _logger.LogInformation("Items sync triggered via API by {User}", User.Identity?.Name ?? "unknown");
        var result = await _wikiSync.SyncItemsAsync(ct);
        return Ok(result);
    }

    /// <summary>Returns the last recorded sync timestamps.</summary>
    [HttpGet("sync/status")]
    public async Task<ActionResult<WikiSyncStatusDto>> GetStatus(CancellationToken ct)
    {
        var blueprintSync = await _db.SyncMetadatas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TaskName == "WikiBlueprints", ct);

        var itemSync = await _db.SyncMetadatas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TaskName == "WikiItems", ct);

        return Ok(new WikiSyncStatusDto
        {
            LastBlueprintSync = blueprintSync?.LastCompletedAt?.ToString("O"),
            LastItemSync = itemSync?.LastCompletedAt?.ToString("O"),
            BlueprintSyncSuccess = blueprintSync?.IsSuccess ?? false,
            ItemSyncSuccess = itemSync?.IsSuccess ?? false
        });
    }
}

public class WikiSyncStatusDto
{
    public string? LastBlueprintSync { get; set; }
    public string? LastItemSync { get; set; }
    public bool BlueprintSyncSuccess { get; set; }
    public bool ItemSyncSuccess { get; set; }
}
