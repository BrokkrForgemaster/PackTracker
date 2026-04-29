namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminAuditLogEntryDto(
    string Action,
    string TargetType,
    string TargetId,
    string Summary,
    string Severity,
    string? BeforeJson,
    string? AfterJson,
    string? CorrelationId = null);
