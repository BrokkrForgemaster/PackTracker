using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAdminAssistanceRequestDetail;

public sealed record GetAdminAssistanceRequestDetailQuery(Guid Id) : IRequest<AdminRequestDetailDto?>;

public sealed class GetAdminAssistanceRequestDetailQueryHandler
    : IRequestHandler<GetAdminAssistanceRequestDetailQuery, AdminRequestDetailDto?>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAdminAssistanceRequestDetailQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<AdminRequestDetailDto?> Handle(
        GetAdminAssistanceRequestDetailQuery request,
        CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.DashboardView, cancellationToken);

        var entity = await _db.AssistanceRequests
            .AsNoTracking()
            .Include(x => x.CreatedByProfile)
            .Include(x => x.AssignedToProfile)
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return null;

        var claims = await _db.RequestClaims
            .AsNoTracking()
            .Where(c => c.RequestId == entity.Id && c.RequestType == "Assistance")
            .Join(_db.Profiles,
                c => c.ProfileId,
                p => p.Id,
                (c, p) => new { DisplayName = p.DiscordDisplayName ?? p.Username, c.ClaimedAt })
            .ToListAsync(cancellationToken);

        var claimDtos = claims
            .Select(c => new AdminRequestClaimDto(c.DisplayName, ToDateTimeOffset(c.ClaimedAt)))
            .ToList();

        var assigneeName = entity.AssignedToProfile is not null
            ? entity.AssignedToProfile.DiscordDisplayName ?? entity.AssignedToProfile.Username
            : claimDtos.Count > 0 ? claimDtos[0].DisplayName : null;

        return new AdminRequestDetailDto(
            entity.Id,
            "Assistance",
            entity.Title,
            entity.Description,
            entity.Status.ToString(),
            entity.Priority.ToString(),
            entity.IsPinned,
            entity.CreatedByProfile is not null
                ? entity.CreatedByProfile.DiscordDisplayName ?? entity.CreatedByProfile.Username
                : null,
            assigneeName,
            ToDateTimeOffset(entity.CreatedAt),
            ToNullableDateTimeOffset(entity.UpdatedAt),
            ToNullableDateTimeOffset(entity.CompletedAt),
            entity.MeetingLocation,
            entity.RewardOffered,
            null,
            (decimal?)entity.QuantityNeeded,
            null,
            null,
            null,
            claimDtos);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? ToNullableDateTimeOffset(DateTime? value) =>
        value.HasValue ? ToDateTimeOffset(value.Value) : null;
}
