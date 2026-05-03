namespace PackTracker.Application.Admin.DTOs;

public sealed record AwardRibbonRequestDto(
    string RibbonName,
    string RibbonDescription,
    string? RibbonImagePath,
    List<Guid> ProfileIds,
    string? Citation);
