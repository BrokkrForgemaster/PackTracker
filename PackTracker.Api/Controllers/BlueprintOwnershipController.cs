using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/blueprints/{blueprintId:guid}/ownership")]
public class BlueprintOwnershipController : ControllerBase
{
    private readonly AppDbContext _db;

    public BlueprintOwnershipController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Register(Guid blueprintId, [FromBody] RegisterBlueprintOwnershipRequest request, CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(discordId))
            return Unauthorized();

        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        if (profile is null)
            return Unauthorized();

        var blueprintExists = await _db.Blueprints.AnyAsync(x => x.Id == blueprintId, ct);
        if (!blueprintExists)
            return NotFound(new { error = "Blueprint not found." });

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
        return Ok(new { message = "Blueprint ownership registered.", ownershipId = existing.Id });
    }
}
