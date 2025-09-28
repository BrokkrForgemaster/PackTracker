using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using System.Security.Claims;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profiles;

    public ProfilesController(IProfileService profiles) => _profiles = profiles;

    // ✅ NEW: Get the currently logged-in user's profile
    [HttpGet("me")]
    [Authorize] // requires valid login/JWT
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        // Pull Discord ID from claims
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return Unauthorized("Missing Discord ID claim.");

        // Load from DB
        var profile = await _profiles.GetByDiscordIdAsync(discordId);
        if (profile is null)
            return NotFound("Profile not found in database.");

        return Ok(profile);
    }

    /// <summary>
    /// Create or update a profile using Discord identity data.
    /// </summary>
    [HttpPost("upsert")]
    [ProducesResponseType(typeof(Profile), StatusCodes.Status200OK)]
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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("discord/{discordId}")]
    public async Task<IActionResult> GetByDiscordId(string discordId)
    {
        var profile = await _profiles.GetByDiscordIdAsync(discordId);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("name/{username}")]
    public async Task<IActionResult> GetByUsername(string username)
    {
        var profile = await _profiles.GetByNameAsync(username);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var profiles = await _profiles.GetAllAsync();
        return Ok(profiles);
    }
}

public class UpsertProfileRequest
{
    public string DiscordId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
