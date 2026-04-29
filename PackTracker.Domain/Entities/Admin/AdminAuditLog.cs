namespace PackTracker.Domain.Entities.Admin;

public class AdminAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorProfileId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string Severity { get; set; } = "Info";
    public string? CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Profile? ActorProfile { get; set; }
}
