namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminSettingsDto(
    bool OperationsEnabled,
    bool MedalAnnouncementsEnabled,
    bool RecruitingPostsEnabled,
    string? OperationsChannelId,
    string? MedalAnnouncementsChannelId,
    string? RecruitingPostsChannelId,
    DateTime UpdatedAt);
