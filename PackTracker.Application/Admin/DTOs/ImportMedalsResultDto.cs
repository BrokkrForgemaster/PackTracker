namespace PackTracker.Application.Admin.DTOs;

public sealed record ImportMedalsResultDto(
    int MedalDefinitionsCreated,
    int MedalDefinitionsUpdated,
    int AwardsCreated,
    int AwardsSkipped,
    IReadOnlyList<string> UnmatchedRecipients,
    IReadOnlyList<string> UnknownMedals);
