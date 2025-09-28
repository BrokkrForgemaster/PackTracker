using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "HouseWolfOnly")] // ✅ Require HouseWolf role
public class RegolithController : ControllerBase
{
    private readonly IRegolithService _regolith;
    private readonly ILogger<RegolithController> _log;

    public RegolithController(IRegolithService regolith, ILogger<RegolithController> log)
    {
        _regolith = regolith;
        _log = log;
    }

    /// <summary>
    /// Get the current Regolith profile for the authenticated user.
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        _log.LogInformation("GET /Regolith/profile requested. User={User}", User.Identity?.Name);

        try
        {
            var profile = await _regolith.GetProfileAsync(ct);

            if (profile == null)
            {
                _log.LogWarning("No Regolith profile found for user {User}", User.Identity?.Name);
                return NotFound(new { message = "Regolith profile not found" });
            }

            _log.LogInformation("Returning Regolith profile for {ScName}", profile.ScName);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error fetching Regolith profile for user {User}", User.Identity?.Name);
            return StatusCode(500, new { message = "Failed to fetch Regolith profile" });
        }
    }

    /// <summary>
    /// Get the current refinery jobs for the authenticated user.
    /// </summary>
    [HttpGet("refinery-jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRefineryJobs(CancellationToken ct)
    {
        _log.LogInformation("GET /Regolith/refinery-jobs requested. User={User}", User.Identity?.Name);

        try
        {
            var jobs = await _regolith.GetRefineryJobsAsync(ct);

            _log.LogInformation("Returning {Count} refinery jobs for {User}", jobs.Count, User.Identity?.Name);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error fetching refinery jobs for user {User}", User.Identity?.Name);
            return StatusCode(500, new { message = "Failed to fetch refinery jobs" });
        }
    }
}
