namespace PackTracker.Application.Admin.DTOs;

public sealed record SubmitMedalNominationRequestDto(
    Guid MedalDefinitionId,
    Guid? NomineeProfileId,
    string NomineeName,
    string Citation);
