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
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.CreatedByProfileId == currentProfileId
                     || _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Assistance" && c.ProfileId == currentProfileId))
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = x.Title,
                RequestType = "Assistance",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                IsPinned = x.IsPinned,
                IsRequestedByCurrentUser = x.CreatedByProfileId == currentProfileId,
                IsAssignedToCurrentUser = _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Assistance" && c.ProfileId == currentProfileId),
                IsAvailableToClaim = x.Status == RequestStatus.Open && _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Assistance") < x.MaxClaims,
                RequesterDisplayName = x.CreatedByProfile != null ? (x.CreatedByProfile.DiscordDisplayName ?? x.CreatedByProfile.Username) : "Unknown",
                AssigneeDisplayName = _dbContext.RequestClaims
                    .Where(c => c.RequestId == x.Id && c.RequestType == "Assistance")
                    .Join(_dbContext.Profiles, c => c.ProfileId, p => p.Id, (c, p) => p.DiscordDisplayName ?? p.Username)
                    .FirstOrDefault(),
                CreatedAt = x.CreatedAt,
                MaxClaims = x.MaxClaims,
                ClaimCount = _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Assistance")
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
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == currentProfileId
                     || _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Procurement" && c.ProfileId == currentProfileId))
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = $"Procure: {(x.Material != null ? x.Material.Name : "Material")}",
                RequestType = "Procurement",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                IsPinned = x.IsPinned,
                IsRequestedByCurrentUser = x.RequesterProfileId == currentProfileId,
                IsAssignedToCurrentUser = _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Procurement" && c.ProfileId == currentProfileId),
                IsAvailableToClaim = x.Status == RequestStatus.Open && _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Procurement") < x.MaxClaims,
                RequesterDisplayName = x.RequesterProfile != null ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username) : "Unknown",
                AssigneeDisplayName = _dbContext.RequestClaims
                    .Where(c => c.RequestId == x.Id && c.RequestType == "Procurement")
                    .Join(_dbContext.Profiles, c => c.ProfileId, p => p.Id, (c, p) => p.DiscordDisplayName ?? p.Username)
                    .FirstOrDefault(),
                CreatedAt = x.CreatedAt,
                MaxClaims = x.MaxClaims,
                ClaimCount = _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Procurement")
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

        var allActiveRequests = assistance
            .Concat(crafting)
            .Concat(procurement)
            .ToList();

        return new DashboardSummaryDto
        {
            ActiveRequests = OrderDashboardRequests(allActiveRequests),
            ScheduledGuides = guides,
            PersonalContext = new PersonalContextDto
            {
                MyActiveTasks = OrderPersonalTasks(allActiveRequests
                    .Where(x => x.IsAssignedToCurrentUser)
                    .ToList()),
                MyPendingRequests = OrderPersonalRequests(allActiveRequests
                    .Where(x => x.IsRequestedByCurrentUser)
                    .ToList())
            }
        };
    }

    private static List<ActiveRequestDto> OrderDashboardRequests(IEnumerable<ActiveRequestDto> requests) =>
        requests
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.IsAssignedToCurrentUser)
            .ThenByDescending(x => x.IsRequestedByCurrentUser)
            .ThenByDescending(x => x.IsAvailableToClaim)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

    private static List<ActiveRequestDto> OrderPersonalTasks(IEnumerable<ActiveRequestDto> requests) =>
        requests
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => string.Equals(x.Priority, RequestPriority.Critical.ToString(), StringComparison.Ordinal))
            .ThenByDescending(x => string.Equals(x.Priority, RequestPriority.High.ToString(), StringComparison.Ordinal))
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

    private static List<ActiveRequestDto> OrderPersonalRequests(IEnumerable<ActiveRequestDto> requests) =>
        requests
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.IsAvailableToClaim)
            .ThenByDescending(x => string.Equals(x.Priority, RequestPriority.Critical.ToString(), StringComparison.Ordinal))
            .ThenByDescending(x => string.Equals(x.Priority, RequestPriority.High.ToString(), StringComparison.Ordinal))
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

    private IQueryable<ActiveRequestDto> BuildCraftingActiveRequestsQuery(Guid? currentProfileId) =>
        _dbContext.CraftingRequests
            .AsNoTracking()
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == currentProfileId
                     || _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Crafting" && c.ProfileId == currentProfileId))
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = x.ItemName ?? (x.Blueprint != null ? x.Blueprint.BlueprintName : "Crafting Request"),
                RequestType = "Crafting",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                IsPinned = x.IsPinned,
                IsRequestedByCurrentUser = x.RequesterProfileId == currentProfileId,
                IsAssignedToCurrentUser = _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Crafting" && c.ProfileId == currentProfileId),
                IsAvailableToClaim = x.Status == RequestStatus.Open && _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Crafting") < x.MaxClaims,
                RequesterDisplayName = x.RequesterProfile != null ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username) : "Unknown",
                AssigneeDisplayName = _dbContext.RequestClaims
                    .Where(c => c.RequestId == x.Id && c.RequestType == "Crafting")
                    .Join(_dbContext.Profiles, c => c.ProfileId, p => p.Id, (c, p) => p.DiscordDisplayName ?? p.Username)
                    .FirstOrDefault(),
                CreatedAt = x.CreatedAt,
                MaxClaims = x.MaxClaims,
                ClaimCount = _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Crafting")
            });

    private IQueryable<ActiveRequestDto> BuildLegacyCraftingActiveRequestsQuery(Guid? currentProfileId) =>
        _dbContext.CraftingRequests
            .AsNoTracking()
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == currentProfileId
                     || _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Crafting" && c.ProfileId == currentProfileId))
            .Select(x => new ActiveRequestDto
            {
                Id = x.Id,
                Title = x.Blueprint != null ? x.Blueprint.BlueprintName : "Crafting Request",
                RequestType = "Crafting",
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                IsPinned = x.IsPinned,
                IsRequestedByCurrentUser = x.RequesterProfileId == currentProfileId,
                IsAssignedToCurrentUser = _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Crafting" && c.ProfileId == currentProfileId),
                IsAvailableToClaim = x.Status == RequestStatus.Open && _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Crafting") < x.MaxClaims,
                RequesterDisplayName = x.RequesterProfile != null ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username) : "Unknown",
                AssigneeDisplayName = _dbContext.RequestClaims
                    .Where(c => c.RequestId == x.Id && c.RequestType == "Crafting")
                    .Join(_dbContext.Profiles, c => c.ProfileId, p => p.Id, (c, p) => p.DiscordDisplayName ?? p.Username)
                    .FirstOrDefault(),
                CreatedAt = x.CreatedAt,
                MaxClaims = x.MaxClaims,
                ClaimCount = _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Crafting")
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
