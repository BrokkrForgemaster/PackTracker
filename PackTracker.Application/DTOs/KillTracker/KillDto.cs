using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.KillTracker;

public class KillDto
{
    public int Id { get; set; }
    public string? Attacker { get; set; } = string.Empty;
    public string? Target { get; set; } = string.Empty;
    public string? Weapon { get; set; } = string.Empty;
    public string? Location { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.MinValue;
    public string GameLogSource { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? UserDisplayName { get; set; } = null;
    public string? Summary { get; set; } = null;
    public bool IsSynced { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Type { get; set; } 
    public KillType KillType { get; set; } 
    public bool IsPvp { get; set; } 
}