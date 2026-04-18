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
        var currentUsername = _currentUser.DisplayName;

        return await _db.MaterialProcurementRequests
            .AsNoTracking()
            .Include(x => x.Material)
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedToProfile)
            .OrderByDescending(x => x.CreatedAt)
            .Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed)
            .Where(x => x.Status == RequestStatus.Open
                     || x.RequesterProfile!.Username == currentUsername
                     || x.AssignedToProfile!.Username == currentUsername)
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
                AssignedToUsername = x.AssignedToProfile != null ? x.AssignedToProfile.Username : null,
                QuantityRequested = x.QuantityRequested,
                QuantityDelivered = x.QuantityDelivered,
                MinimumQuality = x.MinimumQuality,
                PreferredForm = x.PreferredForm.ToString(),
                Priority = x.Priority.ToString(),
                Status = x.Status.ToString(),
                DeliveryLocation = x.DeliveryLocation,
                NumberOfHelpersNeeded = x.NumberOfHelpersNeeded,
                RewardOffered = x.RewardOffered,
                Notes = x.Notes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
