using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAuditLogs;

public sealed record GetAuditLogDetailQuery(Guid Id) : IRequest<AdminAuditLogDetailDto?>;

public sealed class GetAuditLogDetailQueryHandler : IRequestHandler<GetAuditLogDetailQuery, AdminAuditLogDetailDto?>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAuditLogDetailQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<AdminAuditLogDetailDto?> Handle(GetAuditLogDetailQuery request, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.AuditView, cancellationToken);

        var log = await _db.AdminAuditLogs
            .AsNoTracking()
            .Include(x => x.ActorProfile)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (log == null) return null;

        return new AdminAuditLogDetailDto(
            log.Id,
            log.ActorProfile?.Username ?? "System",
            log.Action,
            log.TargetType,
            log.TargetId,
            log.Summary,
            log.Severity,
            log.OccurredAt,
            log.BeforeJson,
            log.AfterJson,
            log.CorrelationId,
            log.Exception,
            log.MachineName,
            log.Environment);
    }
}
