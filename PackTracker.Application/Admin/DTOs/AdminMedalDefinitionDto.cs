namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminMedalDefinitionDto(
    Guid Id,
    string Name,
    string Description,
    string? ImagePath,
    string SourceSystem,
    int DisplayOrder,
    int AwardCount);
