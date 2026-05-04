using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Profiles;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Api.Controllers;

/// <summary>
/// Controller responsible for managing user profiles.
/// </summary>
[ApiController]
[Route("api/v1/profiles")]
public class ProfilesController : ControllerBase
{
    #region Fields

    private readonly IProfileService _profiles;
    private readonly IApplicationDbContext _db;
    private readonly IHouseWolfProfileService _houseWolf;
    private readonly ILogger<ProfilesController> _logger;

    #endregion

    #region Constructor

    public ProfilesController(
        IProfileService profiles,
        IApplicationDbContext db,
        IHouseWolfProfileService houseWolf,
        ILogger<ProfilesController> logger)
    {
        _profiles = profiles;
        _db = db;
        _houseWolf = houseWolf;
        _logger = logger;
    }

    #endregion

    #region Current User

    /// <summary>
    /// Retrieves the currently authenticated user's profile.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(discordId))
        {
            _logger.LogWarning("Missing Discord ID claim.");
            return Unauthorized();
        }

        var profile = await _profiles.GetByDiscordIdAsync(discordId, ct);

        if (profile == null)
        {
            _logger.LogWarning("Profile not found for DiscordId={DiscordId}", discordId);
            return NotFound();
        }

        // Sync with HouseWolf profile data (image, bio, division, etc.)
        try
        {
            var hwProfile = await _houseWolf.GetProfileByDiscordIdAsync(discordId);
            if (hwProfile != null)
            {
                var changed = false;

                if (!string.IsNullOrWhiteSpace(hwProfile.ImageUrl) && profile.ShowcaseImageUrl != hwProfile.ImageUrl)
                {
                    profile.ShowcaseImageUrl = hwProfile.ImageUrl;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(hwProfile.Bio) && profile.ShowcaseBio != hwProfile.Bio)
                {
                    profile.ShowcaseBio = hwProfile.Bio;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(hwProfile.SubDivision) && profile.ShowcaseEyebrow != hwProfile.SubDivision)
                {
                    profile.ShowcaseEyebrow = hwProfile.SubDivision;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(hwProfile.Division) && profile.ShowcaseTagline != hwProfile.Division)
                {
                    profile.ShowcaseTagline = hwProfile.Division;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(hwProfile.CharacterName) && profile.DiscordDisplayName != hwProfile.CharacterName)
                {
                    profile.DiscordDisplayName = hwProfile.CharacterName;
                    changed = true;
                }

                if (changed)
                {
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Synced HouseWolf profile data for DiscordId={DiscordId}", discordId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync HouseWolf profile for DiscordId={DiscordId}. Continuing with existing data.", discordId);
        }

        // Fall back to JWT claims if the DB profile hasn't been synced yet.
        var effectiveDivision = profile.DiscordDivision
            ?? User.FindFirstValue("urn:discord:division");

        var effectiveRank = !string.IsNullOrWhiteSpace(profile.DiscordRank)
            ? profile.DiscordRank
            : User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role
                         && c.Value != PackTracker.Domain.Security.SecurityConstants.Roles.HouseWolfMember)
                .Select(c => c.Value)
                .FirstOrDefault();

        var medals = await _db.MedalAwards
            .AsNoTracking()
            .Where(x => x.ProfileId == profile.Id)
            .Include(x => x.MedalDefinition)
            .OrderBy(x => x.MedalDefinition.DisplayOrder)
            .ThenBy(x => x.MedalDefinition.Name)
            .Select(x => new CurrentProfileMedalDto(
                x.MedalDefinitionId,
                x.MedalDefinition.Name,
                x.MedalDefinition.Description,
                x.MedalDefinition.ImagePath,
                x.Citation,
                x.AwardedAt,
                x.MedalDefinition.AwardType))
            .ToListAsync(ct);

        return Ok(MapCurrentProfile(profile, effectiveRank, effectiveDivision, medals));
    }

    [HttpPut("me/showcase")]
    [Authorize]
    public async Task<IActionResult> UpdateMyShowcase([FromBody] UpdateMyShowcaseRequestDto request, CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(discordId))
        {
            _logger.LogWarning("Missing Discord ID claim during showcase update.");
            return Unauthorized();
        }

        var profile = await _profiles.UpdateShowcaseAsync(
            discordId,
            request.ShowcaseImageUrl,
            request.ShowcaseEyebrow,
            request.ShowcaseTagline,
            request.ShowcaseBio,
            ct);

        if (profile == null)
        {
            _logger.LogWarning("Profile not found for showcase update. DiscordId={DiscordId}", discordId);
            return NotFound();
        }

        var effectiveDivision = profile.DiscordDivision
            ?? User.FindFirstValue("urn:discord:division");

        var effectiveRank = !string.IsNullOrWhiteSpace(profile.DiscordRank)
            ? profile.DiscordRank
            : User.Claims
                .Where(c => c.Type == ClaimTypes.Role
                         && c.Value != PackTracker.Domain.Security.SecurityConstants.Roles.HouseWolfMember)
                .Select(c => c.Value)
                .FirstOrDefault();

        var medals = await _db.MedalAwards
            .AsNoTracking()
            .Where(x => x.ProfileId == profile.Id)
            .Include(x => x.MedalDefinition)
            .OrderBy(x => x.MedalDefinition.DisplayOrder)
            .ThenBy(x => x.MedalDefinition.Name)
            .Select(x => new CurrentProfileMedalDto(
                x.MedalDefinitionId,
                x.MedalDefinition.Name,
                x.MedalDefinition.Description,
                x.MedalDefinition.ImagePath,
                x.Citation,
                x.AwardedAt,
                x.AwardType))
            .ToListAsync(ct);

        return Ok(MapCurrentProfile(profile, effectiveRank, effectiveDivision, medals));
    }

    #endregion

    #region Upsert

    /// <summary>
    /// Creates or updates a profile using Discord identity.
    /// </summary>
    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert([FromBody] UpsertProfileRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var token = await HttpContext.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Missing access token during profile upsert.");
            return Unauthorized();
        }

        var profile = await _profiles.UpsertFromDiscordAsync(
            token,
            request.DiscordId,
            request.Username,
            request.AvatarUrl,
            ct);

        if (profile == null)
        {
            _logger.LogWarning("User not in required guild. DiscordId={DiscordId}", request.DiscordId);
            return Forbid();
        }

        return Ok(profile);
    }

    #endregion

    #region Queries

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var profile = await _profiles.GetByIdAsync(id, ct);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpGet("discord/{discordId}")]
    public async Task<IActionResult> GetByDiscordId(string discordId, CancellationToken ct)
    {
        var profile = await _profiles.GetByDiscordIdAsync(discordId, ct);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpGet("name/{username}")]
    public async Task<IActionResult> GetByUsername(string username, CancellationToken ct)
    {
        var profile = await _profiles.GetByNameAsync(username, ct);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var profiles = await _profiles.GetAllAsync(ct);
        return Ok(profiles);
    }

    [HttpGet("online")]
    [Authorize]
    public async Task<IActionResult> GetOnline(CancellationToken ct)
    {
        var profiles = await _profiles.GetOnlineAsync(ct);
        return Ok(profiles);
    }

    #endregion

    private static CurrentProfileDto MapCurrentProfile(
        Profile profile,
        string? effectiveRank,
        string? effectiveDivision,
        IReadOnlyList<CurrentProfileMedalDto> medals) =>
        new(
            profile.Id,
            profile.DiscordId,
            profile.Username,
            profile.Discriminator,
            profile.DiscordDisplayName,
            effectiveRank,
            effectiveDivision,
            profile.DiscordAvatarUrl,
            profile.IsOnline,
            profile.LastSeenAt,
            profile.CreatedAt,
            profile.LastLogin,
            profile.ShowcaseImageUrl,
            profile.ShowcaseEyebrow,
            profile.ShowcaseTagline,
            profile.ShowcaseBio,
            medals);
}
