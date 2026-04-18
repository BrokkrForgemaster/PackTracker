using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Dashboard.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto?>;

public sealed class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardSummaryQueryHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<DashboardSummaryDto?> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return null;
        }

        var profileId = profile.Id;

        var assistance = await _dbContext.AssistanceRequests
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var crafting = await _dbContext.CraftingRequests
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var procurement = await _dbContext.MaterialProcurementRequests
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var guides = await _dbContext.GuideRequests
            .AsNoTracking()
            .Where(x => x.Status == "Scheduled" || x.Status == "In Progress")
            .OrderBy(x => x.CreatedAt)
            .Select(x => new GuideRequestDto
            {
                Id = x.Id,
                Title = x.Title,
                Status = x.Status,
                ScheduledAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DashboardSummaryDto
        {
            ActiveRequests = assistance.Concat(crafting).Concat(procurement)
                .OrderByDescending(x => x.CreatedAt)
                .ToList(),
            ScheduledGuides = guides
        };
    }
}
