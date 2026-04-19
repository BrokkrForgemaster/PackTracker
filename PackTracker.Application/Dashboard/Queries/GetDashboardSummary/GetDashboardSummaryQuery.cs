using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Dashboard.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto?>;

public sealed class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetDashboardSummaryQueryHandler> _logger;

    public GetDashboardSummaryQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<GetDashboardSummaryQueryHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<DashboardSummaryDto?> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var currentProfileId = await _dbContext.Profiles
            .AsNoTracking()
            .Where(x => x.DiscordId == _currentUser.UserId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var assistance = await _dbContext.AssistanceRequests
            .AsNoTracking()
            .Include(x => x.CreatedByProfile)
            .Include(x => x.AssignedToProfile)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.CreatedByProfileId == currentProfileId
                     || x.AssignedToProfileId == currentProfileId)
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = x.Title,
                RequestType = "Assistance",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                IsPinned = x.IsPinned,
                IsRequestedByCurrentUser = x.CreatedByProfileId == currentProfileId,
                IsAssignedToCurrentUser = x.AssignedToProfileId == currentProfileId,
                IsAvailableToClaim = x.Status == RequestStatus.Open,
                RequesterDisplayName = x.CreatedByProfile != null ? (x.CreatedByProfile.DiscordDisplayName ?? x.CreatedByProfile.Username) : "Unknown",
                AssigneeDisplayName = x.AssignedToProfile != null ? (x.AssignedToProfile.DiscordDisplayName ?? x.AssignedToProfile.Username) : null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ActiveRequestDto> crafting;
        try
        {
            crafting = await BuildCraftingActiveRequestsQuery(currentProfileId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsLegacyCraftingSchemaFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "Dashboard crafting summary failed with newer crafting columns; retrying with legacy-safe projection.");

            crafting = await BuildLegacyCraftingActiveRequestsQuery(currentProfileId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var procurement = await _dbContext.MaterialProcurementRequests
            .AsNoTracking()
            .Include(x => x.Material)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedToProfile)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == currentProfileId
                     || x.AssignedToProfileId == currentProfileId)
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = $"Procure: {(x.Material != null ? x.Material.Name : "Material")}",
                RequestType = "Procurement",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                IsPinned = false,
                IsRequestedByCurrentUser = x.RequesterProfileId == currentProfileId,
                IsAssignedToCurrentUser = x.AssignedToProfileId == currentProfileId,
                IsAvailableToClaim = x.Status == RequestStatus.Open,
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
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.IsAssignedToCurrentUser)
                .ThenByDescending(x => x.IsRequestedByCurrentUser)
                .ThenByDescending(x => x.IsAvailableToClaim)
                .ThenByDescending(x => x.CreatedAt)
                .ToList(),
            ScheduledGuides = guides
        };
    }

    private IQueryable<ActiveRequestDto> BuildCraftingActiveRequestsQuery(Guid? currentProfileId) =>
        _dbContext.CraftingRequests
            .AsNoTracking()
            .Include(x => x.Blueprint)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedCrafterProfile)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == currentProfileId
                     || x.AssignedCrafterProfileId == currentProfileId)
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = x.ItemName ?? (x.Blueprint != null ? x.Blueprint.BlueprintName : "Crafting Request"),
                RequestType = "Crafting",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                IsPinned = false,
                IsRequestedByCurrentUser = x.RequesterProfileId == currentProfileId,
                IsAssignedToCurrentUser = x.AssignedCrafterProfileId == currentProfileId,
                IsAvailableToClaim = x.Status == RequestStatus.Open,
                RequesterDisplayName = x.RequesterProfile != null ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username) : "Unknown",
                AssigneeDisplayName = x.AssignedCrafterProfile != null ? (x.AssignedCrafterProfile.DiscordDisplayName ?? x.AssignedCrafterProfile.Username) : null,
                CreatedAt = x.CreatedAt
            });

    private IQueryable<ActiveRequestDto> BuildLegacyCraftingActiveRequestsQuery(Guid? currentProfileId) =>
        _dbContext.CraftingRequests
            .AsNoTracking()
            .Include(x => x.Blueprint)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedCrafterProfile)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == currentProfileId
                     || x.AssignedCrafterProfileId == currentProfileId)
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = x.Blueprint != null ? x.Blueprint.BlueprintName : "Crafting Request",
                RequestType = "Crafting",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                IsPinned = false,
                IsRequestedByCurrentUser = x.RequesterProfileId == currentProfileId,
                IsAssignedToCurrentUser = x.AssignedCrafterProfileId == currentProfileId,
                IsAvailableToClaim = x.Status == RequestStatus.Open,
                RequesterDisplayName = x.RequesterProfile != null ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username) : "Unknown",
                AssigneeDisplayName = x.AssignedCrafterProfile != null ? (x.AssignedCrafterProfile.DiscordDisplayName ?? x.AssignedCrafterProfile.Username) : null,
                CreatedAt = x.CreatedAt
            });

    private static bool IsLegacyCraftingSchemaFailure(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("ItemName", StringComparison.OrdinalIgnoreCase)
               || message.Contains("MaterialSupplyMode", StringComparison.OrdinalIgnoreCase)
               || message.Contains("RequesterTimeZoneDisplayName", StringComparison.OrdinalIgnoreCase)
               || message.Contains("RequesterUtcOffsetMinutes", StringComparison.OrdinalIgnoreCase)
               || message.Contains("column", StringComparison.OrdinalIgnoreCase)
                  && message.Contains("CraftingRequests", StringComparison.OrdinalIgnoreCase);
    }
}
