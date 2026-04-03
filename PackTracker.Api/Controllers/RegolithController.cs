using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Controllers;

/// <summary name="RegolithController">
/// Controller for managing Regolith-related endpoints.
/// Handles operations such as retrieving user profiles and refinery jobs.
/// Requires authentication and authorization.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "HouseWolfOnly")]
internal class RegolithController : ControllerBase
{
    #region Fields and Constructor
    private readonly IRegolithService _regolith;
    private readonly ILogger<RegolithController> _logger;

    public RegolithController(IRegolithService regolith, ILogger<RegolithController> logger)
    {
        _regolith = regolith;
        _logger = logger;
    }
    #endregion

    #region Endpoints
    /// <summary name="GetProfile">
    /// Get the current Regolith profile for the authenticated user.
    /// Returns 404 if no profile exists. Requires authentication.
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "Unknown";
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Request: GET /Regolith/profile initiated by {User}", user);

        try
        {
            ct.ThrowIfCancellationRequested();

            var profile = await _regolith.GetProfileAsync(ct);

            if (profile is null)
            {
                _logger.LogWarning("No Regolith profile found for {User}", user);
                return NotFound(new ProblemDetails
                {
                    Title = "Profile not found",
                    Detail = "No Regolith profile found for this account.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            stopwatch.Stop();
            _logger.LogInformation("Success: Regolith profile returned for {ScName} in {Elapsed}ms", profile.ScName, stopwatch.ElapsedMilliseconds);

            return Ok(profile);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request canceled: GET /Regolith/profile by {User}", user);
            return Problem("Request canceled.", statusCode: StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error fetching Regolith profile for {User}", user);
            return Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred while retrieving your Regolith profile.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary name="GetRefineryJobs">
    /// Get the current refinery jobs for the authenticated user.
    /// </summary>
    [HttpGet("refinery-jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRefineryJobs(CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "Unknown";
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Request: GET /Regolith/refinery-jobs initiated by {User}", user);

        try
        {
            ct.ThrowIfCancellationRequested();

            var jobs = await _regolith.GetRefineryJobsAsync(ct);
            stopwatch.Stop();

            if (jobs is null || jobs.Count == 0)
            {
                _logger.LogInformation("No refinery jobs found for {User}", user);
                return Ok(Array.Empty<object>());
            }

            _logger.LogInformation("Success: {Count} refinery jobs returned for {User} in {Elapsed}ms",
                jobs.Count, user, stopwatch.ElapsedMilliseconds);

            return Ok(jobs);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request canceled: GET /Regolith/refinery-jobs by {User}", user);
            return Problem("Request canceled.", statusCode: StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error fetching refinery jobs for {User}", user);
            return Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred while retrieving refinery jobs.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    #endregion
}
