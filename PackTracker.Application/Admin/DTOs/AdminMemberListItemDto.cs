namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminMemberListItemDto(
    Guid ProfileId,
    string Username,
    string? DisplayName,
    string DiscordId,
    string? DiscordRank,
    string? DiscordDivision,
    string? ShowcaseImageUrl,
    string? ShowcaseEyebrow,
    string? ShowcaseTagline,
    string? ShowcaseBio,
    DateTime JoinDate,
    DateTime LastLogin,
    IReadOnlyCollection<string> ActiveAdminRoles);
