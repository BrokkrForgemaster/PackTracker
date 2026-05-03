namespace PackTracker.Application.Admin.DTOs;

public sealed record AwardRibbonRequestDto(
    string RibbonName,
    string RibbonDescription,
    string? RibbonImagePath,
    string? RibbonPublicImageUrl,
    List<Guid> ProfileIds,
    string? Citation);
