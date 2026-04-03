namespace PackTracker.Application.DTOs.Regolith;

public class RegolithRefineryJobDtoRaw
{
    public string JobId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? Progress { get; set; }
    public double? Yield { get; set; }
    public double? Efficiency { get; set; }
    public long? Eta { get; set; }
    public long SubmittedAt { get; set; }
    public long? CompletedAt { get; set; }
}
