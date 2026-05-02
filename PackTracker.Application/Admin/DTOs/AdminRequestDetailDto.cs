namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminRequestDetailDto(
    Guid Id,
    string RequestType,
    string Title,
    string? Description,
    string Status,
    string Priority,
    bool IsPinned,
    string? RequesterDisplayName,
    string? AssigneeDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ClosedAt,
    string? Location,
    string? RewardOffered,
    string? RefusalReason,
    decimal? QuantityRequested,
    decimal? QuantityDelivered,
    int? MinimumQuality,
    string? MaterialSupplyMode,
    IReadOnlyList<AdminRequestClaimDto> Claims);
