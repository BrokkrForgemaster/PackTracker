namespace PackTracker.Application.Admin.DTOs;

public sealed record UpdateAdminSettingsRequestDto(
    bool OperationsEnabled,
    bool MedalAnnouncementsEnabled,
    bool RecruitingPostsEnabled,
    string? OperationsChannelId,
    string? MedalAnnouncementsChannelId,
    string? RecruitingPostsChannelId);
