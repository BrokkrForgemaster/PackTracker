using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Wiki;
using PackTracker.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using PackTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace PackTracker.Api.Controllers;

/// <summary name="WikiSyncController">
/// Controller responsible for handling API requests related to synchronizing data from the Star Citizen Wiki.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/wiki")]
public class WikiSyncController : ControllerBase
{
    #region Properties
    private readonly IWikiSyncService _wikiSync;
    private readonly AppDbContext _db;
    private readonly ILogger<WikiSyncController> _logger;
    #endregion
    
    #region Constructors
    public WikiSyncController(IWikiSyncService wikiSync, AppDbContext db, ILogger<WikiSyncController> logger)
    {
        _wikiSync = wikiSync;
        _db = db;
        _logger = logger;
    }
    #endregion
    
    #region Endpoints
    [HttpPost("sync/blueprints")]
    public async Task<ActionResult<WikiSyncResult>> SyncBlueprints(CancellationToken ct)
    {
        _logger.LogInformation("Blueprint sync triggered via API by {User}", User.Identity?.Name ?? "unknown");
        var result = await _wikiSync.SyncBlueprintsAsync(ct);
        return Ok(result);
    }
    
    [HttpPost("sync/items")]
    public async Task<ActionResult<WikiSyncResult>> SyncItems(CancellationToken ct)
    {
        _logger.LogInformation("Items sync triggered via API by {User}", User.Identity?.Name ?? "unknown");
        var result = await _wikiSync.SyncItemsAsync(ct);
        return Ok(result);
    }
    
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
    #endregion
}


