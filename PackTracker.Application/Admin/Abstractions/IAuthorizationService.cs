namespace PackTracker.Application.Admin.Abstractions;

public interface IAuthorizationService
{
    Task RequireAdminAccessAsync(CancellationToken ct = default);
    Task RequirePermissionAsync(string permissionKey, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(string permissionKey, CancellationToken ct = default);
}
