using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Entities;
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
    public async Task<IActionResult> Register(Guid blueprintId, [FromBody] RegisterBlueprintOwnershipRequest request, CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(discordId))
        {
            _logger.LogWarning("Blueprint ownership registration rejected — no DiscordId claim.");
            return Unauthorized();
        }

        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        if (profile is null)
        {
            _logger.LogWarning("Blueprint ownership registration rejected — profile not found for DiscordId={DiscordId}.", discordId);
            return Unauthorized();
        }

        var blueprintExists = await _db.Blueprints.AnyAsync(x => x.Id == blueprintId, ct);
        if (!blueprintExists)
        {
            // Auto-create a placeholder so wiki UUIDs can be used for ownership tracking
            _db.Blueprints.Add(new PackTracker.Domain.Entities.Blueprint
            {
                Id = blueprintId,
                Slug = blueprintId.ToString(),
                BlueprintName = "Wiki Blueprint",
                CraftedItemName = "Unknown",
                Category = "Unknown",
                IsInGameAvailable = true,
                DataConfidence = "WikiAPI",
                WikiUuid = blueprintId.ToString()
            });
            await _db.SaveChangesAsync(ct);
        }

        var existing = await _db.MemberBlueprintOwnerships
            .FirstOrDefaultAsync(x => x.BlueprintId == blueprintId && x.MemberProfileId == profile.Id, ct);

        if (existing is null)
        {
            existing = new MemberBlueprintOwnership
            {
                BlueprintId = blueprintId,
                MemberProfileId = profile.Id,
                InterestType = request.InterestType,
                AvailabilityStatus = string.IsNullOrWhiteSpace(request.AvailabilityStatus) ? "Available" : request.AvailabilityStatus.Trim(),
                Notes = request.Notes
            };
            _db.MemberBlueprintOwnerships.Add(existing);
        }
        else
        {
            existing.InterestType = request.InterestType;
            existing.AvailabilityStatus = string.IsNullOrWhiteSpace(request.AvailabilityStatus) ? existing.AvailabilityStatus : request.AvailabilityStatus.Trim();
            existing.Notes = request.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Blueprint ownership registered. BlueprintId={BlueprintId} Profile={Username} InterestType={InterestType} Status={Status}",
            blueprintId, profile.Username, request.InterestType, existing.AvailabilityStatus);

        return Ok(new { message = "Blueprint ownership registered.", ownershipId = existing.Id });
    }
}
