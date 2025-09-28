namespace PackTracker.Domain.Entities;

// Before
// Missing RegolithProfile class and DbSet<RegolithProfile> in AppDbContext

// After
public class RegolithProfile
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ScName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SyncedAt { get; set; }
}


