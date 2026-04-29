namespace PackTracker.Domain.Entities.Admin;

public class AdminPermissionAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminRoleId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AdminRole? AdminRole { get; set; }
}
