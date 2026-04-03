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

    // Material/Resource fields
    public string? MaterialName { get; set; }
    public int? QuantityNeeded { get; set; }
    public string? MeetingLocation { get; set; }
    public string? RewardOffered { get; set; }
    public int? NumberOfHelpersNeeded { get; set; }
}