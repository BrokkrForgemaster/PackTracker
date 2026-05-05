namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminAuditLogDetailDto(
    Guid Id,
    string ActorDisplayName,
    string Action,
    string TargetType,
    string TargetId,
    string Summary,
    string Severity,
    DateTime OccurredAt,
    string? BeforeJson,
    string? AfterJson,
    string? CorrelationId,
    string? Exception,
    string? MachineName,
    string? Environment);
