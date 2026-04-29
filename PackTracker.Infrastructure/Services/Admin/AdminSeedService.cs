using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities.Admin;
using PackTracker.Domain.Security;

namespace PackTracker.Infrastructure.Services.Admin;

public sealed class AdminSeedService
{
    private readonly IAdminDbContext _db;

    public AdminSeedService(IAdminDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await EnsureRolesAsync(ct);
        await EnsurePermissionsAsync(ct);
        await EnsureSettingsRowAsync(ct);
    }

    private async Task EnsureRolesAsync(CancellationToken ct)
    {
        var required = new (string Name, AdminTier Tier)[]
        {
            (AdminRoleNames.Captain, AdminTier.Admin),
            (AdminRoleNames.FleetCommander, AdminTier.SeniorAdmin),
            (AdminRoleNames.Armor, AdminTier.SeniorAdmin),
            (AdminRoleNames.HighCouncilor, AdminTier.ExecutiveAdmin),
            (AdminRoleNames.HandOfTheClan, AdminTier.SuperAdmin),
            (AdminRoleNames.ClanWarlord, AdminTier.SuperAdmin)
        };

        foreach (var item in required)
        {
            var existing = await _db.AdminRoles.FirstOrDefaultAsync(x => x.Name == item.Name, ct);
            if (existing is null)
            {
                _db.AdminRoles.Add(new AdminRole
                {
                    Name = item.Name,
                    Tier = item.Tier,
                    IsSystemRole = true
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsurePermissionsAsync(CancellationToken ct)
    {
        var roles = await _db.AdminRoles.ToDictionaryAsync(x => x.Name, x => x.Id, ct);

        var grants = new Dictionary<string, string[]>
        {
            [AdminRoleNames.Captain] =
            [
                AdminPermissions.AdminAccess,
                AdminPermissions.DashboardView,
                AdminPermissions.SettingsView,
                AdminPermissions.MembersView
            ],
            [AdminRoleNames.FleetCommander] =
            [
                AdminPermissions.AdminAccess,
                AdminPermissions.DashboardView,
                AdminPermissions.SettingsView,
                AdminPermissions.MembersView
            ],
            [AdminRoleNames.Armor] =
            [
                AdminPermissions.AdminAccess,
                AdminPermissions.DashboardView,
                AdminPermissions.SettingsView,
                AdminPermissions.MembersView
            ],
            [AdminRoleNames.HighCouncilor] =
            [
                AdminPermissions.AdminAccess,
                AdminPermissions.DashboardView,
                AdminPermissions.SettingsView,
                AdminPermissions.MembersView,
                AdminPermissions.AuditView
            ],
            [AdminRoleNames.HandOfTheClan] =
            [
                AdminPermissions.AdminAccess,
                AdminPermissions.DashboardView,
                AdminPermissions.SettingsView,
                AdminPermissions.SettingsDiscordManage,
                AdminPermissions.SettingsSystemManage,
                AdminPermissions.MembersView,
                AdminPermissions.MembersRolesManage,
                AdminPermissions.AuditView,
                AdminPermissions.AuditFullView,
                AdminPermissions.RecordsArchive,
                AdminPermissions.RecordsDelete,
                AdminPermissions.ApprovalsOverride
            ],
            [AdminRoleNames.ClanWarlord] =
            [
                AdminPermissions.AdminAccess,
                AdminPermissions.DashboardView,
                AdminPermissions.SettingsView,
                AdminPermissions.SettingsDiscordManage,
                AdminPermissions.SettingsSystemManage,
                AdminPermissions.MembersView,
                AdminPermissions.MembersRolesManage,
                AdminPermissions.AuditView,
                AdminPermissions.AuditFullView,
                AdminPermissions.RecordsArchive,
                AdminPermissions.RecordsDelete,
                AdminPermissions.ApprovalsOverride
            ]
        };

        foreach (var grant in grants)
        {
            var roleId = roles[grant.Key];
            foreach (var permissionKey in grant.Value)
            {
                var exists = await _db.AdminPermissionAssignments.AnyAsync(
                    x => x.AdminRoleId == roleId && x.PermissionKey == permissionKey,
                    ct);

                if (!exists)
                {
                    _db.AdminPermissionAssignments.Add(new AdminPermissionAssignment
                    {
                        AdminRoleId = roleId,
                        PermissionKey = permissionKey
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureSettingsRowAsync(CancellationToken ct)
    {
        if (await _db.DiscordIntegrationSettings.AnyAsync(ct))
        {
            return;
        }

        _db.DiscordIntegrationSettings.Add(new DiscordIntegrationSetting());
        await _db.SaveChangesAsync(ct);
    }
}
