namespace PackTracker.Application.DTOs.Regolith;


public class RegolithProfileDtoRaw
{
    public string UserId { get; set; } = string.Empty;
    public string ScName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}