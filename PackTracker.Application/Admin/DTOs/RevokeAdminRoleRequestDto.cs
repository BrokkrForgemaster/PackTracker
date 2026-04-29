namespace PackTracker.Application.Admin.DTOs;

public sealed record RevokeAdminRoleRequestDto(
    Guid ProfileId,
    Guid AdminRoleId,
    string? Notes);
