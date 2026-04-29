using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Security;

namespace PackTracker.Infrastructure.Services.Admin;

public sealed class RbacService : IRbacService
{
    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RbacService(IAdminDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CurrentAdminContext> GetCurrentAdminContextAsync(CancellationToken ct = default)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, ct);
        if (profile is null)
        {
            return new CurrentAdminContext(Guid.Empty, _currentUser.DisplayName, [], [], null, false);
        }

        var assignments = await _db.MemberRoleAssignments
            .Include(x => x.AdminRole)
            .ThenInclude(x => x!.PermissionAssignments)
            .Where(x => x.ProfileId == profile.Id && x.RevokedAt == null)
            .ToListAsync(ct);

        var roles = assignments
            .Where(x => x.AdminRole is not null)
            .Select(x => x.AdminRole!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissions = assignments
            .Where(x => x.AdminRole is not null)
            .SelectMany(x => x.AdminRole!.PermissionAssignments.Select(p => p.PermissionKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var highestTier = assignments
            .Where(x => x.AdminRole is not null)
            .Select(x => (AdminTier?)x.AdminRole!.Tier)
            .OrderByDescending(x => x)
            .FirstOrDefault();

        return new CurrentAdminContext(
            profile.Id,
            profile.DiscordDisplayName ?? profile.Username,
            roles,
            permissions,
            highestTier,
            permissions.Contains(AdminPermissions.AdminAccess, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsForProfileAsync(Guid profileId, CancellationToken ct = default)
    {
        return await _db.MemberRoleAssignments
            .Where(x => x.ProfileId == profileId && x.RevokedAt == null)
            .SelectMany(x => x.AdminRole!.PermissionAssignments.Select(p => p.PermissionKey))
            .Distinct()
            .ToArrayAsync(ct);
    }

    public async Task<bool> HasPermissionAsync(Guid profileId, string permissionKey, CancellationToken ct = default)
    {
        var permissions = await GetPermissionsForProfileAsync(profileId, ct);
        return permissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
    }
}
