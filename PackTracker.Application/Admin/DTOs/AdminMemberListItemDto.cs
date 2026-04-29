namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminMemberListItemDto(
    Guid ProfileId,
    string Username,
    string? DisplayName,
    string DiscordId,
    string? DiscordRank,
    DateTime JoinDate,
    DateTime LastLogin,
    IReadOnlyCollection<string> ActiveAdminRoles);
