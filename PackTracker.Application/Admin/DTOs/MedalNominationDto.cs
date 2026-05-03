namespace PackTracker.Application.Admin.DTOs;

public sealed record MedalNominationDto(
    Guid Id,
    Guid MedalDefinitionId,
    string MedalName,
    string? MedalImagePath,
    Guid? NomineeProfileId,
    string NomineeName,
    string NominatorName,
    string Citation,
    string Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewedByName,
    string? ReviewNotes);
