namespace PackTracker.Application.DTOs.Regolith;

public class RegolithProfileDto
{
    public string UserId { get; set; } = string.Empty;
    public string ScName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}