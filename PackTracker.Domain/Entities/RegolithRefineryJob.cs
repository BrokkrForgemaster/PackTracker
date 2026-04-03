namespace PackTracker.Domain.Entities;

public class RegolithRefineryJob
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    
    public string Progress { get; set; } = string.Empty;
    
    public string Efficiency { get; set; } = string.Empty;
    
    public string Yield { get; set; } = string.Empty;
    
    public string Eta { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime SyncedAt { get; set; }
}