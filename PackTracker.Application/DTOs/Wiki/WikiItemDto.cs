namespace PackTracker.Application.DTOs.Wiki;

public class WikiItemDto
{
    public string Uuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Class { get; set; }
    public string? Category { get; set; }
}
