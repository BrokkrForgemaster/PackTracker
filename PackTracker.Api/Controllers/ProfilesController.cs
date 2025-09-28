using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profiles;

    public ProfilesController(IProfileService profiles) => _profiles = profiles;

    /// <summary>
    /// Create or update a profile using Discord identity data.
    /// </summary>
    [HttpPost("upsert")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert([FromBody] UpsertProfileRequest request)
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("Missing access token");

        var profile = await _profiles.UpsertFromDiscordAsync(
            token,
            request.DiscordId,
            request.Username,
            request.AvatarUrl);

        if (profile is null)
            return Forbid("Not a member of House Wolf");

        return Ok(profile);
    }


    /// <summary>
    /// Get a profile by its internal GUID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// Get a profile by Discord ID.
    /// </summary>
    [HttpGet("discord/{discordId}")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByDiscordId(string discordId)
    {
        var profile = await _profiles.GetByDiscordIdAsync(discordId);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// Get a profile by username (case-insensitive).
    /// </summary>
    [HttpGet("name/{username}")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByUsername(string username)
    {
        var profile = await _profiles.GetByNameAsync(username);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// Get all profiles.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Profile>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var profiles = await _profiles.GetAllAsync();
        return Ok(profiles);
    }
}

/// <summary>
/// Request body for upserting a profile.
/// </summary>
public class UpsertProfileRequest
{
    public string DiscordId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
