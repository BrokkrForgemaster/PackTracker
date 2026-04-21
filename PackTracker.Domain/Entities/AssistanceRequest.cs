using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

/// <summary>
/// Represents a general assistance request posted by a member to the org.
/// </summary>
public class AssistanceRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public RequestKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public bool IsPinned { get; set; }
    public Guid CreatedByProfileId { get; set; }
    public Guid? AssignedToProfileId { get; set; }
    public string? MaterialName { get; set; }
    public int? QuantityNeeded { get; set; }
    public string? MeetingLocation { get; set; }
    public string? RewardOffered { get; set; }
    public int MaxClaims { get; set; } = 1;
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Profile? CreatedByProfile { get; set; }
    public Profile? AssignedToProfile { get; set; }
}
