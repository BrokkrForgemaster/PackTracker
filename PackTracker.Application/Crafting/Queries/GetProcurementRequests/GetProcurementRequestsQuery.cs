using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Queries.GetProcurementRequests;

public sealed record GetProcurementRequestsQuery : IRequest<IReadOnlyList<MaterialProcurementRequestListItemDto>>;

public sealed class GetProcurementRequestsQueryHandler : IRequestHandler<GetProcurementRequestsQuery, IReadOnlyList<MaterialProcurementRequestListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetProcurementRequestsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MaterialProcurementRequestListItemDto>> Handle(GetProcurementRequestsQuery request, CancellationToken cancellationToken)
    {
        var currentProfileId = await _db.Profiles
            .AsNoTracking()
            .Where(x => x.DiscordId == _currentUser.UserId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return await _db.MaterialProcurementRequests
            .AsNoTracking()
            .Include(x => x.Material)
            .Include(x => x.RequesterProfile)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
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
                AssignedToUsername = _db.RequestClaims
                    .Where(c => c.RequestId == x.Id && c.RequestType == "Procurement")
                    .Include(c => c.Profile)
                    .Select(c => c.Profile != null ? (c.Profile.DiscordDisplayName ?? c.Profile.Username) : "User")
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
    }
}
