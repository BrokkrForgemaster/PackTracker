namespace PackTracker.Domain.Security;

public static class AdminPermissions
{
    public const string AdminAccess = "admin.access";
    public const string DashboardView = "dashboard.view";
    public const string SettingsView = "settings.view";
    public const string SettingsDiscordManage = "settings.discord.manage";
    public const string SettingsSystemManage = "settings.system.manage";
    public const string AuditView = "audit.view";
    public const string AuditFullView = "audit.full.view";
    public const string MembersView = "members.view";
    public const string MembersRolesManage = "members.roles.manage";
    public const string MedalsView = "medals.view";
    public const string MedalsManage = "medals.manage";
    public const string RecordsArchive = "records.archive";
    public const string RecordsDelete = "records.delete";
    public const string ApprovalsOverride = "approvals.override";
}
