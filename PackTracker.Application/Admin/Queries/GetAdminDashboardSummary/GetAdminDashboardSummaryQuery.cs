using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAdminDashboardSummary;

public sealed record GetAdminDashboardSummaryQuery() : IRequest<AdminDashboardSummaryDto>;

public sealed class GetAdminDashboardSummaryQueryHandler : IRequestHandler<GetAdminDashboardSummaryQuery, AdminDashboardSummaryDto>
{
    private readonly IAdminDbContext _db;
    private readonly IRbacService _rbac;
    private readonly IAuthorizationService _authorization;

    public GetAdminDashboardSummaryQueryHandler(
        IAdminDbContext db,
        IRbacService rbac,
        IAuthorizationService authorization)
    {
        _db = db;
        _rbac = rbac;
        _authorization = authorization;
    }

    public async Task<AdminDashboardSummaryDto> Handle(GetAdminDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.DashboardView, cancellationToken);
        var context = await _rbac.GetCurrentAdminContextAsync(cancellationToken);

        var totalMembers = await _db.Profiles.CountAsync(cancellationToken);
        var activeAssignments = await _db.MemberRoleAssignments.CountAsync(x => x.RevokedAt == null, cancellationToken);
        var totalAuditEntries = await _db.AdminAuditLogs.CountAsync(cancellationToken);
        var settings = await _db.DiscordIntegrationSettings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminDashboardSummaryDto(
            context.CanAccessAdmin,
            context.HighestTier?.ToString(),
            totalMembers,
            activeAssignments,
            totalAuditEntries,
            settings is not null,
            settings?.UpdatedAt);
    }
}
