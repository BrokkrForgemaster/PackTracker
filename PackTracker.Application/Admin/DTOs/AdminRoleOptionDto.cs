namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminRoleOptionDto(
    Guid RoleId,
    string Name,
    string Tier);
