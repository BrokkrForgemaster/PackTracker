using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using PackTracker.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using PackTracker.Application.DTOs.Profiles;

namespace PackTracker.Api.Controllers;

/// <summary name="ProfilesController">
/// Controller for managing user profiles.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class ProfilesController : ControllerBase
{
    #region Fields and Constructor
    private readonly IProfileService _profiles;
    private readonly ILogger<ProfilesController> _logger;

    public ProfilesController(IProfileService profiles, ILogger<ProfilesController> logger)
    {
        _profiles = profiles;
        _logger = logger;
    }
    #endregion

    #region Endpoints
    /// <summary name="GetMe">
    /// Get the currently logged-in user's profile using their Discord identity.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        try
        {
            var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(discordId))
            {
                _logger.LogWarning("Missing Discord ID claim for user request.");
                return Unauthorized("Missing Discord ID claim.");
            }

            var profile = await _profiles.GetByDiscordIdAsync(discordId, ct);
            if (profile is null)
            {
                _logger.LogInformation("Profile not found for DiscordId {DiscordId}", discordId);
                return NotFound("Profile not found in database.");
            }

            _logger.LogInformation("Fetched profile for DiscordId {DiscordId}", discordId);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile for current user.");
            return Problem("An unexpected error occurred while retrieving your profile.");
        }
    }

    /// <summary name="Upsert">
    /// Create or update a profile using Discord identity data.
    /// </summary>
    [HttpPost("upsert")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upsert([FromBody] UpsertProfileRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid profile upsert request received.");
            return ValidationProblem(ModelState);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.DiscordId) || string.IsNullOrWhiteSpace(request.Username))
            {
                _logger.LogWarning("Invalid upsert payload: missing required fields.");
                return BadRequest("DiscordId and Username are required.");
            }

            var token = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Profile upsert attempt without access token.");
                return Unauthorized("Missing access token.");
            }

            var profile = await _profiles.UpsertFromDiscordAsync(token, request.DiscordId, request.Username, request.AvatarUrl, ct);
            if (profile is null)
            {
                _logger.LogWarning("Upsert denied for DiscordId {DiscordId}: Not a member of House Wolf.", request.DiscordId);
                return Forbid("Not a member of House Wolf.");
            }

            _logger.LogInformation("Profile upsert successful for DiscordId {DiscordId}.", request.DiscordId);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during profile upsert for DiscordId {DiscordId}.", request.DiscordId);
            return Problem("An error occurred while updating your profile.");
        }
    }

    /// <summary name="GetById">
    /// Get a profile by its unique ID.
    /// </summary>
    /// <param name="id">
    /// The GUID of the profile to retrieve.
    /// </param>
    /// <param name="ct">
    /// Cancellation token to cancel the operation if needed.
    /// </param>
    /// <returns>
    /// The profile if found, otherwise a 404 Not Found response.
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var profile = await _profiles.GetByIdAsync(id, ct);
            return profile is null ? NotFound() : Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching profile by ID {ProfileId}", id);
            return Problem("Error retrieving profile by ID.");
        }
    }

    /// <summary name="GetByDiscordId">
    /// Get a profile by its Discord ID.
    /// </summary>
    [HttpGet("discord/{discordId}")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByDiscordId(string discordId, CancellationToken ct)
    {
        try
        {
            var profile = await _profiles.GetByDiscordIdAsync(discordId, ct);
            return profile is null ? NotFound() : Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching profile by DiscordId {DiscordId}", discordId);
            return Problem("Error retrieving profile by DiscordId.");
        }
    }

    /// <summary name="GetByUsername">
    /// Get a profile by its username.
    /// </summary>
    [HttpGet("name/{username}")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByUsername(string username, CancellationToken ct)
    {
        try
        {
            var profile = await _profiles.GetByNameAsync(username, ct);
            return profile is null ? NotFound() : Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching profile by username {Username}", username);
            return Problem("Error retrieving profile by username.");
        }
    }

    /// <summary name="GetAll">
    /// Get all profiles in the system.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Profile>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        try
        {
            var profiles = await _profiles.GetAllAsync(ct);
            _logger.LogInformation("Retrieved {Count} profiles.", profiles.Count());
            return Ok(profiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all profiles.");
            return Problem("Error retrieving profiles list.");
        }
    }
    #endregion
}

