using PackTracker.Application.Admin.Common;

namespace PackTracker.Application.Admin.Abstractions;

public interface IRbacService
{
    Task<CurrentAdminContext> GetCurrentAdminContextAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> GetPermissionsForProfileAsync(Guid profileId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid profileId, string permissionKey, CancellationToken ct = default);
}
