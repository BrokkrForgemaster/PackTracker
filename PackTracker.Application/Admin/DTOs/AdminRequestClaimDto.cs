namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminRequestClaimDto(
    string DisplayName,
    DateTimeOffset ClaimedAt);
