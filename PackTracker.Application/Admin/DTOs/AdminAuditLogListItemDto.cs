namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminAuditLogListItemDto(
    Guid Id,
    string ActorDisplayName,
    string Action,
    string TargetType,
    string TargetId,
    string Summary,
    string Severity,
    DateTime OccurredAt);
