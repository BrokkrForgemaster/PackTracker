namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminRequestHistoryItemDto(
    Guid Id,
    string RequestType,
    string Title,
    string Status,
    string Priority,
    string? RequesterDisplayName,
    string? AssigneeDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsPinned);
