namespace PackTracker.Application.DTOs.Regolith;

public class RegolithRefineryJobDtoRaw
{
    public string JobId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public long SubmittedAt { get; set; }
    public long? CompletedAt { get; set; }
}