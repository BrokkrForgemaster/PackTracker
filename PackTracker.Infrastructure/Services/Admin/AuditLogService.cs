using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.Common;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities.Admin;

namespace PackTracker.Infrastructure.Services.Admin;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IAdminDbContext _db;
    private readonly IRbacService _rbac;

    public AuditLogService(IAdminDbContext db, IRbacService rbac)
    {
        _db = db;
        _rbac = rbac;
    }

    public async Task WriteAsync(AdminAuditLogEntryDto entry, CancellationToken ct = default)
    {
        var context = await _rbac.GetCurrentAdminContextAsync(ct);
        if (context.ProfileId == Guid.Empty)
        {
            throw new AdminAuthorizationException("Cannot write admin audit log without a resolved profile.");
        }

        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            ActorProfileId = context.ProfileId,
            Action = entry.Action,
            TargetType = entry.TargetType,
            TargetId = entry.TargetId,
            Summary = entry.Summary,
            Severity = entry.Severity,
            BeforeJson = entry.BeforeJson,
            AfterJson = entry.AfterJson,
            CorrelationId = entry.CorrelationId,
            OccurredAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
