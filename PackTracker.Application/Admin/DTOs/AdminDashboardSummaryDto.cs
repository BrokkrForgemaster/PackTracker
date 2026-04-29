namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminDashboardSummaryDto(
    bool CanAccessAdmin,
    string? HighestTier,
    int TotalMembers,
    int ActiveAdminRoleAssignments,
    int TotalAuditEntries,
    bool DiscordSettingsConfigured,
    DateTime? LastSettingsUpdatedAt);
