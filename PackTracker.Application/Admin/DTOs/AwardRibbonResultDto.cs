namespace PackTracker.Application.Admin.DTOs;

public sealed record AwardRibbonResultDto(
    Guid AwardId,
    string RibbonName,
    string RecipientName,
    bool AlreadyAwarded);
