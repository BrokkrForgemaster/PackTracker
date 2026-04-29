using PackTracker.Application.Admin.DTOs;

namespace PackTracker.Application.Admin.Abstractions;

public interface IAuditLogService
{
    Task WriteAsync(AdminAuditLogEntryDto entry, CancellationToken ct = default);
}
