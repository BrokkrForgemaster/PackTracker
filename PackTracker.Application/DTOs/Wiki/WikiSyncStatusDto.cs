namespace PackTracker.Application.DTOs.Wiki;

public class WikiSyncStatusDto
{
    public string? LastBlueprintSync { get; set; }
    public string? LastItemSync { get; set; }
    public bool BlueprintSyncSuccess { get; set; }
    public bool ItemSyncSuccess { get; set; }
}