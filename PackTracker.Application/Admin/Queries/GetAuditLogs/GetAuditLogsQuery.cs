using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAuditLogs;

public sealed record GetAuditLogsQuery(int Take = 100) : IRequest<IReadOnlyList<AdminAuditLogListItemDto>>;

public sealed class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, IReadOnlyList<AdminAuditLogListItemDto>>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAuditLogsQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<AdminAuditLogListItemDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.AuditView, cancellationToken);

        return await _db.AdminAuditLogs
            .AsNoTracking()
            .Include(x => x.ActorProfile)
            .OrderByDescending(x => x.OccurredAt)
            .Take(request.Take)
            .Select(x => new AdminAuditLogListItemDto(
                x.Id,
                x.ActorProfile != null ? x.ActorProfile.Username : "Unknown",
                x.Action,
                x.TargetType,
                x.TargetId,
                x.Summary,
                x.Severity,
                x.OccurredAt))
            .ToListAsync(cancellationToken);
    }
}
