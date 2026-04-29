using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Security;

namespace PackTracker.Infrastructure.Services.Admin;

public sealed class RbacService : IRbacService
{
    private sealed record ResolvedAdminAccess(
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Permissions,
        AdminTier? HighestTier);

    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RbacService> _logger;

    public RbacService(IAdminDbContext db, ICurrentUserService currentUser, ILogger<RbacService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<CurrentAdminContext> GetCurrentAdminContextAsync(CancellationToken ct = default)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, ct);
        if (profile is null)
        {
            _logger.LogWarning(
                "Admin RBAC did not find a profile. CurrentUserId={CurrentUserId}, DisplayName={DisplayName}",
                _currentUser.UserId,
                _currentUser.DisplayName);
            return new CurrentAdminContext(Guid.Empty, _currentUser.DisplayName, [], [], null, false);
        }

        var resolved = await ResolveAdminAccessAsync(profile, ct);
        var canAccessAdmin = resolved.Permissions.Contains(AdminPermissions.AdminAccess, StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation(
            "Admin RBAC final result. CurrentUserId={CurrentUserId}, ProfileId={ProfileId}, DiscordRank={DiscordRank}, HighestTier={HighestTier}, CanAccessAdmin={CanAccessAdmin}",
            _currentUser.UserId,
            profile.Id,
            profile.DiscordRank ?? "<null>",
            resolved.HighestTier?.ToString() ?? "<null>",
            canAccessAdmin);

        return new CurrentAdminContext(
            profile.Id,
            profile.DiscordDisplayName ?? profile.Username,
            resolved.Roles,
            resolved.Permissions,
            resolved.HighestTier,
            canAccessAdmin);
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsForProfileAsync(Guid profileId, CancellationToken ct = default)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.Id == profileId, ct);
        if (profile is null)
        {
            return [];
        }

        var resolved = await ResolveAdminAccessAsync(profile, ct);
        return resolved.Permissions;
    }

    public async Task<bool> HasPermissionAsync(Guid profileId, string permissionKey, CancellationToken ct = default)
    {
        var permissions = await GetPermissionsForProfileAsync(profileId, ct);
        return permissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ResolvedAdminAccess> ResolveAdminAccessAsync(Domain.Entities.Profile profile, CancellationToken ct)
    {
        var assignments = await _db.MemberRoleAssignments
            .Include(x => x.AdminRole)
            .ThenInclude(x => x!.PermissionAssignments)
            .Where(x => x.ProfileId == profile.Id && x.RevokedAt == null)
            .ToListAsync(ct);
        _logger.LogInformation(
            "Admin RBAC resolving. ProfileId={ProfileId}, DiscordRank={DiscordRank}, ExplicitAssignments={ExplicitAssignments}",
            profile.Id,
            profile.DiscordRank ?? "<null>",
            assignments.Count);

        if (assignments.Count > 0)
        {
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

            _logger.LogInformation(
                "Admin RBAC used explicit assignments. ProfileId={ProfileId}, HighestTier={HighestTier}",
                profile.Id,
                highestTier?.ToString() ?? "<null>");
            return new ResolvedAdminAccess(roles, permissions, highestTier);
        }

        if (string.IsNullOrWhiteSpace(profile.DiscordRank))
        {
            _logger.LogWarning("Admin RBAC found no Discord rank and no explicit assignments. ProfileId={ProfileId}", profile.Id);
            return new ResolvedAdminAccess([], [], null);
        }

        var fallbackRole = await _db.AdminRoles
            .Include(x => x.PermissionAssignments)
            .FirstOrDefaultAsync(
                x => x.Name == profile.DiscordRank,
                ct);

        if (fallbackRole is null)
        {
            _logger.LogWarning(
                "Admin RBAC fallback failed. ProfileId={ProfileId}, DiscordRank={DiscordRank}",
                profile.Id,
                profile.DiscordRank);
            return new ResolvedAdminAccess([], [], null);
        }

        var fallbackPermissions = fallbackRole.PermissionAssignments
            .Select(x => x.PermissionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _logger.LogInformation(
            "Admin RBAC used DiscordRank fallback. ProfileId={ProfileId}, DiscordRank={DiscordRank}, ResolvedRole={ResolvedRole}, HighestTier={HighestTier}",
            profile.Id,
            profile.DiscordRank,
            fallbackRole.Name,
            fallbackRole.Tier);

        return new ResolvedAdminAccess(
            [fallbackRole.Name],
            fallbackPermissions,
            fallbackRole.Tier);
    }
}
