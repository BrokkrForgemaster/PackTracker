namespace PackTracker.Application.DTOs.Profiles;

public sealed record CurrentProfileDto(
    Guid Id,
    string DiscordId,
    string Username,
    string Discriminator,
    string? DiscordDisplayName,
    string? DiscordRank,
    string? DiscordDivision,
    string? DiscordAvatarUrl,
    bool IsOnline,
    DateTime LastSeenAt,
    DateTime CreatedAt,
    DateTime LastLogin,
    string? ShowcaseImageUrl,
    string? ShowcaseEyebrow,
    string? ShowcaseTagline,
    string? ShowcaseBio,
    IReadOnlyList<CurrentProfileMedalDto> Medals);
