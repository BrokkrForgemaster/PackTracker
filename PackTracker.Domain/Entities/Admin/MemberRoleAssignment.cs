namespace PackTracker.Domain.Entities.Admin;

public class MemberRoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Guid AdminRoleId { get; set; }
    public Guid AssignedByProfileId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? Notes { get; set; }

    public Profile? Profile { get; set; }
    public AdminRole? AdminRole { get; set; }
    public Profile? AssignedByProfile { get; set; }
}
