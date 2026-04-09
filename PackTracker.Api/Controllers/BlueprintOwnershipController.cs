using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/blueprints/{blueprintId:guid}/ownership")]
public class BlueprintOwnershipController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<BlueprintOwnershipController> _logger;

    public BlueprintOwnershipController(AppDbContext db, ILogger<BlueprintOwnershipController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        Guid blueprintId,
        [FromBody] RegisterBlueprintOwnershipRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Ownership register request received. BlueprintId={BlueprintId}, InterestType={InterestType}, AvailabilityStatus={AvailabilityStatus}",
            blueprintId,
            request.InterestType,
            request.AvailabilityStatus);

        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(discordId))
        {
            _logger.LogWarning("Blueprint ownership registration rejected — no DiscordId claim.");
            return Unauthorized();
        }

        _logger.LogInformation("Ownership register user resolved from claims. DiscordId={DiscordId}", discordId);

        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        if (profile is null)
        {
            _logger.LogWarning(
                "Blueprint ownership registration rejected — profile not found for DiscordId={DiscordId}.",
                discordId);

            return Unauthorized();
        }

        _logger.LogInformation(
            "Ownership register profile found. ProfileId={ProfileId}, Username={Username}",
            profile.Id,
            profile.Username);

        var wikiUuidString = blueprintId.ToString();
        var blueprint = await _db.Blueprints.FirstOrDefaultAsync(
            x => x.Id == blueprintId || x.WikiUuid == wikiUuidString, ct);
        if (blueprint is null)
        {
            _logger.LogWarning(
                "Blueprint ownership registration rejected — blueprint {BlueprintId} not found.",
                blueprintId);

            return NotFound(new
            {
                message = "Blueprint not found.",
                blueprintId
            });
        }

        _logger.LogInformation(
            "Ownership register blueprint found. BlueprintId={BlueprintId}, BlueprintName={BlueprintName}, CraftedItemName={CraftedItemName}, WikiUuid={WikiUuid}",
            blueprint.Id,
            blueprint.BlueprintName,
            blueprint.CraftedItemName,
            blueprint.WikiUuid);

        var existing = await _db.MemberBlueprintOwnerships
            .FirstOrDefaultAsync(
                x => x.BlueprintId == blueprint.Id && x.MemberProfileId == profile.Id,
                ct);

        if (existing is null)
        {
            _logger.LogInformation(
                "No existing ownership record found. Creating new one for BlueprintId={BlueprintId}, ProfileId={ProfileId}",
                blueprint.Id,
                profile.Id);

            existing = new MemberBlueprintOwnership
            {
                BlueprintId = blueprint.Id,
                MemberProfileId = profile.Id,
                InterestType = request.InterestType,
                AvailabilityStatus = string.IsNullOrWhiteSpace(request.AvailabilityStatus)
                    ? "Available"
                    : request.AvailabilityStatus.Trim(),
                Notes = request.Notes
            };

            _db.MemberBlueprintOwnerships.Add(existing);
        }
        else
        {
            _logger.LogInformation(
                "Existing ownership record found. OwnershipId={OwnershipId}, OldInterestType={OldInterestType}, OldAvailabilityStatus={OldAvailabilityStatus}",
                existing.Id,
                existing.InterestType,
                existing.AvailabilityStatus);

            existing.InterestType = request.InterestType;
            existing.AvailabilityStatus = string.IsNullOrWhiteSpace(request.AvailabilityStatus)
                ? existing.AvailabilityStatus
                : request.AvailabilityStatus.Trim();
            existing.Notes = request.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        var allOwnershipRows = await _db.MemberBlueprintOwnerships
            .AsNoTracking()
            .Where(x => x.BlueprintId == blueprint.Id)
            .Include(x => x.MemberProfile)
            .OrderBy(x => x.MemberProfile!.Username)
            .Select(x => new
            {
                x.Id,
                x.BlueprintId,
                x.MemberProfileId,
                Username = x.MemberProfile != null ? x.MemberProfile.Username : "Unknown",
                InterestType = x.InterestType.ToString(),
                OwnershipStatus = x.OwnershipStatus.ToString(),
                x.AvailabilityStatus,
                x.VerifiedAt,
                x.UpdatedAt
            })
            .ToListAsync(ct);

        var ownerCount = allOwnershipRows.Count(x =>
            string.Equals(x.InterestType, MemberBlueprintInterestType.Owns.ToString(), StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation(
            "Blueprint ownership saved. BlueprintId={BlueprintId}, TotalOwnershipRows={TotalOwnershipRows}, OwnerCount={OwnerCount}, OwnershipRows={OwnershipRows}",
            blueprintId,
            allOwnershipRows.Count,
            ownerCount,
            System.Text.Json.JsonSerializer.Serialize(allOwnershipRows));

        return Ok(new
        {
            message = "Blueprint ownership registered.",
            ownershipId = existing.Id,
            ownerCount
        });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BlueprintOwnerDto>>> GetOwners(Guid blueprintId, CancellationToken ct)
    {
        _logger.LogInformation("Ownership GET request received. BlueprintId={BlueprintId}", blueprintId);

        var wikiUuidString = blueprintId.ToString();
        var blueprint = await _db.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == blueprintId || x.WikiUuid == wikiUuidString, ct);

        if (blueprint is null)
        {
            _logger.LogWarning("Ownership GET failed. BlueprintId={BlueprintId} not found.", blueprintId);
            return NotFound(new { message = "Blueprint not found.", blueprintId });
        }

        _logger.LogInformation(
            "Ownership GET blueprint found. BlueprintId={BlueprintId}, BlueprintName={BlueprintName}, CraftedItemName={CraftedItemName}, WikiUuid={WikiUuid}",
            blueprint.Id,
            blueprint.BlueprintName,
            blueprint.CraftedItemName,
            blueprint.WikiUuid);

        var owners = await _db.MemberBlueprintOwnerships
            .AsNoTracking()
            .Where(x => x.BlueprintId == blueprint.Id)
            .Include(x => x.MemberProfile)
            .OrderByDescending(x => x.InterestType)
            .ThenBy(x => x.MemberProfile!.Username)
            .Select(x => new BlueprintOwnerDto
            {
                MemberProfileId = x.MemberProfileId,
                Username = x.MemberProfile != null ? x.MemberProfile.Username : "Unknown Member",
                InterestType = x.InterestType.ToString(),
                OwnershipStatus = x.OwnershipStatus.ToString(),
                AvailabilityStatus = x.AvailabilityStatus,
                VerifiedAt = x.VerifiedAt,
                Notes = x.Notes
            })
            .ToListAsync(ct);

        _logger.LogInformation(
            "Ownership GET result. BlueprintId={BlueprintId}, OwnerRowCount={OwnerRowCount}, Owners={OwnersJson}",
            blueprintId,
            owners.Count,
            System.Text.Json.JsonSerializer.Serialize(owners));

        return Ok(owners);
    }
}