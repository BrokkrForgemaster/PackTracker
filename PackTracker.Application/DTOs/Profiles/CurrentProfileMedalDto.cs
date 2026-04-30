namespace PackTracker.Application.DTOs.Profiles;

public sealed record CurrentProfileMedalDto(
    Guid MedalDefinitionId,
    string Name,
    string Description,
    string? ImagePath,
    string? Citation,
    DateTime? AwardedAt);
