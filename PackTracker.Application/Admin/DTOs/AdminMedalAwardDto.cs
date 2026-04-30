namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminMedalAwardDto(
    Guid Id,
    Guid MedalDefinitionId,
    string MedalName,
    Guid? ProfileId,
    string RecipientName,
    string? ProfileDisplayName,
    DateTime? AwardedAt,
    DateTime ImportedAt,
    string SourceSystem);
