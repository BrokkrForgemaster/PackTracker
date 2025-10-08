using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class RequestTicket
{
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public RequestKind Kind { get; set; }
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public RequestStatus Status { get; set; } = RequestStatus.Open;

    public string CreatedByUserId { get; set; } = "";          // from your auth
    public string CreatedByDisplayName { get; set; } = "";
    public string? AssignedToUserId { get; set; }
    public string? AssignedToDisplayName { get; set; }

    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
    public string? CompletedByUserId { get; set; }
}