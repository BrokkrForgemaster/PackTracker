using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOS.Request;

public sealed class RequestDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public RequestKind Kind { get; set; }
    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; }
    public string CreatedByDisplayName { get; set; } = "";
    public string? AssignedToDisplayName { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}