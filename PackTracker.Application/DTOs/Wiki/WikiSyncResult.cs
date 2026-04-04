namespace PackTracker.Application.DTOs.Wiki;

public class WikiSyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
    public string? ErrorMessage { get; set; }
}
