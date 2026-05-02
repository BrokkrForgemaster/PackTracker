using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAdminAssistanceRequestHistory;

public sealed record GetAdminAssistanceRequestHistoryQuery() : IRequest<IReadOnlyList<AdminRequestHistoryItemDto>>;

public sealed class GetAdminAssistanceRequestHistoryQueryHandler
    : IRequestHandler<GetAdminAssistanceRequestHistoryQuery, IReadOnlyList<AdminRequestHistoryItemDto>>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAdminAssistanceRequestHistoryQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<AdminRequestHistoryItemDto>> Handle(
        GetAdminAssistanceRequestHistoryQuery request,
        CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.DashboardView, cancellationToken);

        var items = await _db.AssistanceRequests
            .AsNoTracking()
            .Where(x => x.Status == RequestStatus.Completed
                     || x.Status == RequestStatus.Cancelled
                     || x.Status == RequestStatus.Refused)
            .OrderByDescending(x => x.CompletedAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Title,
                Status = x.Status.ToString(),
                Priority = x.Priority.ToString(),
                RequesterDisplayName = x.CreatedByProfile != null ? (x.CreatedByProfile.DiscordDisplayName ?? x.CreatedByProfile.Username) : null,
                AssignedToDisplayName = x.AssignedToProfile != null ? (x.AssignedToProfile.DiscordDisplayName ?? x.AssignedToProfile.Username) : null,
                ClaimedByDisplayName = _db.RequestClaims
                    .Where(c => c.RequestId == x.Id && c.RequestType == "Assistance")
                    .Join(_db.Profiles, c => c.ProfileId, p => p.Id, (c, p) => p.DiscordDisplayName ?? p.Username)
                    .FirstOrDefault(),
                x.CreatedAt,
                x.UpdatedAt,
                x.CompletedAt,
                x.IsPinned
            })
            .ToListAsync(cancellationToken);

        return items
            .Select(x => new AdminRequestHistoryItemDto(
                x.Id,
                "Assistance",
                x.Title,
                x.Status,
                x.Priority,
                x.RequesterDisplayName,
                x.AssignedToDisplayName ?? x.ClaimedByDisplayName,
                ToDateTimeOffset(x.CreatedAt),
                ToNullableDateTimeOffset(x.CompletedAt ?? x.UpdatedAt),
                x.IsPinned))
            .ToList();
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? ToNullableDateTimeOffset(DateTime? value) =>
        value.HasValue ? ToDateTimeOffset(value.Value) : null;
}
