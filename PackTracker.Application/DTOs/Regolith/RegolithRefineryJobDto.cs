namespace PackTracker.Application.DTOs.Regolith;

public class RegolithRefineryJobDto
{
    public string JobId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public double Efficiency { get; set; }
    public double Progress { get; set; }
    public double Yield { get; set; }
    public DateTime? Eta { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public DateTime SyncedAt { get; set; }
}
