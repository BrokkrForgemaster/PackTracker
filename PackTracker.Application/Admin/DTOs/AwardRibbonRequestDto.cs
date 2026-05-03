namespace PackTracker.Application.Admin.DTOs;

public sealed record AwardRibbonRequestDto(
    string RibbonName,
    string RibbonDescription,
    string? RibbonImagePath,
    Guid? ProfileId,
    string RecipientName,
    string? Citation);
