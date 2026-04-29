using PackTracker.Domain.Security;

namespace PackTracker.Domain.Entities.Admin;

public class AdminRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public AdminTier Tier { get; set; }
    public bool IsSystemRole { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AdminPermissionAssignment> PermissionAssignments { get; set; } = new List<AdminPermissionAssignment>();
    public ICollection<MemberRoleAssignment> MemberAssignments { get; set; } = new List<MemberRoleAssignment>();
}
