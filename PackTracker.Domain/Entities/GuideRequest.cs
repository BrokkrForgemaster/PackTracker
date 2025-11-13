namespace PackTracker.Domain.Entities;

public class GuideRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ulong ThreadId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Requester { get; set; } = string.Empty;
    public string Status { get; set; } = "Scheduled"; // Scheduled / Assigned / Completed / Cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Assignment tracking
    public ulong? AssignedToUserId { get; set; }
    public string? AssignedToUsername { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
