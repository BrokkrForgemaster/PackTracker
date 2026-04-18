namespace PackTracker.Application.Common;

public sealed record StatusUpdateResult(
    bool Success,
    string? Message,
    Guid? RequestId = null,
    string? PreviousStatus = null,
    string? NewStatus = null);
