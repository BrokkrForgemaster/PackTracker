using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Queries.GetProcurementRequests;

public sealed record GetProcurementRequestsQuery : IRequest<IReadOnlyList<MaterialProcurementRequestListItemDto>>;

public sealed class GetProcurementRequestsQueryHandler : IRequestHandler<GetProcurementRequestsQuery, IReadOnlyList<MaterialProcurementRequestListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetProcurementRequestsQueryHandler> _logger;

    public GetProcurementRequestsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<GetProcurementRequestsQueryHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MaterialProcurementRequestListItemDto>> Handle(GetProcurementRequestsQuery request, CancellationToken cancellationToken)
    {
        var currentProfileId = await _db.Profiles
            .AsNoTracking()
            .Where(x => x.DiscordId == _currentUser.UserId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "[DIAGNOSTIC] Identity resolution: DiscordId={DiscordId}, ProfileId={ProfileId}",
            _currentUser.UserId,
            currentProfileId?.ToString() ?? "NULL");

        _logger.LogInformation(
            "[DIAGNOSTIC] Applying procurement filters: ProfileId={ProfileId}, StatusExclusions={Statuses}",
            currentProfileId?.ToString() ?? "NULL",
            "Cancelled, Completed");

        var results = await _db.MaterialProcurementRequests
            .AsNoTracking()
            .Where(x => (x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
                     || x.RequesterProfileId == currentProfileId
                     || x.AssignedToProfileId == currentProfileId
                     || _db.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Procurement" && c.ProfileId == currentProfileId))
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfileId == currentProfileId
                     || x.AssignedToProfileId == currentProfileId
                     || _db.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Procurement" && c.ProfileId == currentProfileId))
            .Select(x => new MaterialProcurementRequestListItemDto
            {
                Id = x.Id,
                MaterialId = x.MaterialId,
                LinkedCraftingRequestId = x.LinkedCraftingRequestId,
                MaterialName = x.Material != null ? x.Material.Name : "Unknown",
                RequesterUsername = x.RequesterProfile != null ? x.RequesterProfile.Username : "Unknown",
                RequesterDisplayName = x.RequesterProfile != null
                    ? (x.RequesterProfile.DiscordDisplayName ?? x.RequesterProfile.Username)
                    : "Unknown",
                AssignedToUsername = x.AssignedToProfile != null
                    ? (x.AssignedToProfile.DiscordDisplayName ?? x.AssignedToProfile.Username)
                    : _db.RequestClaims
                        .Where(c => c.RequestId == x.Id && c.RequestType == "Procurement")
                        .Join(_db.Profiles, c => c.ProfileId, p => p.Id, (c, p) => p.DiscordDisplayName ?? p.Username)
                        .FirstOrDefault(),
                QuantityRequested = x.QuantityRequested,
                QuantityDelivered = x.QuantityDelivered,
                MinimumQuality = x.MinimumQuality,
                PreferredForm = x.PreferredForm.ToString(),
                Priority = x.Priority.ToString(),
                Status = x.Status.ToString(),
                IsPinned = x.IsPinned,
                DeliveryLocation = x.DeliveryLocation,
                MaxClaims = x.MaxClaims,
                ClaimCount = _db.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Procurement"),
                RewardOffered = x.RewardOffered,
                Notes = x.Notes,
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("[DIAGNOSTIC] GetProcurementRequests returned {Count} items", results.Count);
        return results;
    }
}
