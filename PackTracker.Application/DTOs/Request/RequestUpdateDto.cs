using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Request;


public sealed class RequestUpdateDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public RequestKind Kind { get; set; }
    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToDisplayName { get; set; }
    public DateTime? DueAt { get; set; }
}