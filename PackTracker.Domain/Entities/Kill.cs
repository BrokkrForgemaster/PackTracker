using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class Kill
{
    public Guid Id { get; set; }
    public string? Attacker { get; set; }
    public string? Target { get; set; }
    public string? Type { get; set; }
    public string? Summary { get; set; } 
    public bool IsSynced { get; set; } 
    public DateTime CreatedAt { get; set; } 
    public string? Weapon { get; set; } 
    public string? Location { get; set; } 
    public DateTime Timestamp { get; set; }
    public string? GameLogSource { get; set; } 
    public KillType KillType { get; set; } 
    public bool IsPvp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime? SyncedAt { get; set; } 
}