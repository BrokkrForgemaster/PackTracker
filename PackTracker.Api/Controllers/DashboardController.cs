using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(AppDbContext db, ILogger<DashboardController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
    {
        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var profile = await _db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);

        if (profile == null) return Unauthorized();

        var profileId = profile.Id;

        // 1. Assistance Requests
        var assistance = await _db.AssistanceRequests
            .AsNoTracking()
            .Include(x => x.CreatedByProfile)
            .Include(x => x.AssignedToProfile)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.CreatedByProfileId == profileId
                     || x.AssignedToProfileId == profileId)
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = x.Title,
                RequestType = "Assistance",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                RequesterDisplayName = x.CreatedByProfile != null ? (x.CreatedByProfile.DiscordDisplayName ?? x.CreatedByProfile.Username) : "Unknown",
                AssigneeDisplayName = x.AssignedToProfile != null ? (x.AssignedToProfile.DiscordDisplayName ?? x.AssignedToProfile.Username) : null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        // 2. Crafting Requests
        var crafting = await _db.CraftingRequests
            .AsNoTracking()
            .Include(x => x.Blueprint)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedCrafterProfile)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == profileId
                     || x.AssignedCrafterProfileId == profileId)
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = x.ItemName ?? (x.Blueprint != null ? x.Blueprint.BlueprintName : "Crafting Request"),
                RequestType = "Crafting",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                RequesterDisplayName = x.RequesterProfile != null ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username) : "Unknown",
                AssigneeDisplayName = x.AssignedCrafterProfile != null ? (x.AssignedCrafterProfile.DiscordDisplayName ?? x.AssignedCrafterProfile.Username) : null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        // 3. Procurement Requests
        var procurement = await _db.MaterialProcurementRequests
            .AsNoTracking()
            .Include(x => x.Material)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedToProfile)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == profileId
                     || x.AssignedToProfileId == profileId)
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = $"Procure: {(x.Material != null ? x.Material.Name : "Material")}",
                RequestType = "Procurement",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                RequesterDisplayName = x.RequesterProfile != null ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username) : "Unknown",
                AssigneeDisplayName = x.AssignedToProfile != null ? (x.AssignedToProfile.DiscordDisplayName ?? x.AssignedToProfile.Username) : null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        // 4. Guides
        var guides = await _db.GuideRequests
            .AsNoTracking()
            .Where(x => x.Status == "Scheduled" || x.Status == "In Progress")
            .OrderBy(x => x.CreatedAt)
            .Select(x => new GuideRequestDto
            {
                Id = x.Id,
                Title = x.Title,
                Status = x.Status,
                ScheduledAt = x.CreatedAt // Placeholder if no explicit scheduled date exists
            })
            .ToListAsync(ct);

        var summary = new DashboardSummaryDto
        {
            ActiveRequests = assistance.Concat(crafting).Concat(procurement)
                .OrderByDescending(x => x.CreatedAt)
                .ToList(),
            ScheduledGuides = guides
        };

        return Ok(summary);
    }
}
