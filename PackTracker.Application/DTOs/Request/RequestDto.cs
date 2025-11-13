using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Request;

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

    // Material/Resource fields
    public string? MaterialName { get; set; }
    public int? QuantityNeeded { get; set; }
    public string? MeetingLocation { get; set; }
    public string? RewardOffered { get; set; }
    public int? NumberOfHelpersNeeded { get; set; }
}