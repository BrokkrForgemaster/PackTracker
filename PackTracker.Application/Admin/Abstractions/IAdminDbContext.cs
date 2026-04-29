using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities.Admin;

namespace PackTracker.Application.Admin.Abstractions;

public interface IAdminDbContext : IApplicationDbContext
{
    DbSet<AdminRole> AdminRoles { get; }
    DbSet<AdminPermissionAssignment> AdminPermissionAssignments { get; }
    DbSet<MemberRoleAssignment> MemberRoleAssignments { get; }
    DbSet<AdminAuditLog> AdminAuditLogs { get; }
    DbSet<DiscordIntegrationSetting> DiscordIntegrationSettings { get; }
}
