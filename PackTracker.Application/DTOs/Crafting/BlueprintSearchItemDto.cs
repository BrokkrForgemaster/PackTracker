public sealed class BlueprintSearchItemDto
{
    public Guid Id { get; set; }          // local DB blueprint id
    public Guid WikiUuid { get; set; }    // wiki id
    public string BlueprintName { get; set; } = string.Empty;
    public string CraftedItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsInGameAvailable { get; set; }
    public string? AcquisitionSummary { get; set; }
    public string DataConfidence { get; set; } = string.Empty;
    public int VerifiedOwnerCount { get; set; }
}