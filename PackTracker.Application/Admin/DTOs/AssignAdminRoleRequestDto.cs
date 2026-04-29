namespace PackTracker.Application.Admin.DTOs;

public sealed record AssignAdminRoleRequestDto(
    Guid ProfileId,
    Guid AdminRoleId,
    string? Notes);
