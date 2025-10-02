using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

/// <summary name="KillEntity">
/// Represents a kill event in the game, capturing details about the attacker, target, weapon used,
/// and other relevant information.
/// </summary>
public class KillEntity
{
    public Guid Id { get; set; }
    public string? Attacker { get; set; } 
    public string? Target { get; set; } 
    public string? Weapon { get; set; }
    public DateTime Timestamp { get; set; }
    public KillType KillType { get; set; }
    
    public string? Type { get; set; } // e.g., "Air", "Ground", "Vehicle"
    public string? Location { get; set; }
    public bool IsSynced { get; set; } 
    public DateTime CreatedAt { get; set; }
    
    public string? Summary { get; set; } = string.Empty;
}