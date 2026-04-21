namespace PackTracker.Domain.Entities;

public class Blueprint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string BlueprintName { get; set; } = string.Empty;
    public string CraftedItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsInGameAvailable { get; set; } = true;
    public string? AcquisitionSummary { get; set; }
    public string? AcquisitionLocation { get; set; }
    public string? AcquisitionMethod { get; set; }
    public string? SourceVersion { get; set; }
    public string DataConfidence { get; set; } = "Imported";
    public string? Notes { get; set; }
    public string? WikiUuid { get; set; }
    public string? WikiLastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public int OwnerCount { get; set; } 

    public BlueprintRecipe? Recipe { get; set; }
}
