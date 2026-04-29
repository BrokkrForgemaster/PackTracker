using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.Common;

namespace PackTracker.Infrastructure.Services.Admin;

public sealed class AuthorizationService : IAuthorizationService
{
    private readonly IRbacService _rbac;

    public AuthorizationService(IRbacService rbac)
    {
        _rbac = rbac;
    }

    public async Task RequireAdminAccessAsync(CancellationToken ct = default)
    {
        var context = await _rbac.GetCurrentAdminContextAsync(ct);
        if (!context.CanAccessAdmin)
        {
            throw new AdminAuthorizationException("Admin access is required.");
        }
    }

    public async Task RequirePermissionAsync(string permissionKey, CancellationToken ct = default)
    {
        var context = await _rbac.GetCurrentAdminContextAsync(ct);
        if (!context.CanAccessAdmin || !context.Permissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new AdminAuthorizationException($"Missing required admin permission: {permissionKey}");
        }
    }

    public async Task<bool> HasPermissionAsync(string permissionKey, CancellationToken ct = default)
    {
        var context = await _rbac.GetCurrentAdminContextAsync(ct);
        return context.Permissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
    }
}
