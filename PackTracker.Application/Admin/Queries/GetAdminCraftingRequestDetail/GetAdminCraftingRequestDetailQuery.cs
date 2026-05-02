using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAdminCraftingRequestDetail;

public sealed record GetAdminCraftingRequestDetailQuery(Guid Id) : IRequest<AdminRequestDetailDto?>;

public sealed class GetAdminCraftingRequestDetailQueryHandler
    : IRequestHandler<GetAdminCraftingRequestDetailQuery, AdminRequestDetailDto?>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAdminCraftingRequestDetailQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<AdminRequestDetailDto?> Handle(
        GetAdminCraftingRequestDetailQuery request,
        CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.DashboardView, cancellationToken);

        var entity = await _db.CraftingRequests
            .AsNoTracking()
            .Include(x => x.RequesterProfile)
            .Include(x => x.AssignedCrafterProfile)
            .Include(x => x.Blueprint)
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return null;

        var claims = await _db.RequestClaims
            .AsNoTracking()
            .Where(c => c.RequestId == entity.Id && c.RequestType == "Crafting")
            .Join(_db.Profiles,
                c => c.ProfileId,
                p => p.Id,
                (c, p) => new { DisplayName = p.DiscordDisplayName ?? p.Username, c.ClaimedAt })
            .ToListAsync(cancellationToken);

        var claimDtos = claims
            .Select(c => new AdminRequestClaimDto(c.DisplayName, ToDateTimeOffset(c.ClaimedAt)))
            .ToList();

        var assigneeName = entity.AssignedCrafterProfile is not null
            ? entity.AssignedCrafterProfile.DiscordDisplayName ?? entity.AssignedCrafterProfile.Username
            : claimDtos.Count > 0 ? claimDtos[0].DisplayName : null;

        var title = entity.ItemName ?? (entity.Blueprint?.BlueprintName ?? "Crafting Request");

        return new AdminRequestDetailDto(
            entity.Id,
            "Crafting",
            title,
            entity.Notes,
            entity.Status.ToString(),
            entity.Priority.ToString(),
            entity.IsPinned,
            entity.RequesterProfile is not null
                ? entity.RequesterProfile.DiscordDisplayName ?? entity.RequesterProfile.Username
                : null,
            assigneeName,
            ToDateTimeOffset(entity.CreatedAt),
            ToNullableDateTimeOffset(entity.UpdatedAt),
            ToNullableDateTimeOffset(entity.CompletedAt),
            entity.DeliveryLocation,
            entity.RewardOffered,
            entity.RefusalReason,
            (decimal?)entity.QuantityRequested,
            null,
            entity.MinimumQuality,
            entity.MaterialSupplyMode.ToString(),
            claimDtos);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? ToNullableDateTimeOffset(DateTime? value) =>
        value.HasValue ? ToDateTimeOffset(value.Value) : null;
}
