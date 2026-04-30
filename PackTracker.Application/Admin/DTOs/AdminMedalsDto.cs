namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminMedalsDto(
    IReadOnlyList<AdminMedalDefinitionDto> AvailableMedals,
    IReadOnlyList<AdminMedalAwardDto> Awards);
