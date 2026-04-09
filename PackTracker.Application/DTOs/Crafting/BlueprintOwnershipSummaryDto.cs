namespace PackTracker.Application.DTOs.Crafting;

public sealed class BlueprintOwnershipSummaryDto
{
    public Guid Id { get; set; }          // local DB blueprint id
    public Guid WikiUuid { get; set; }    // wiki blueprint id
    public string BlueprintName { get; set; } = string.Empty;
    public string CraftedItemName { get; set; } = string.Empty;
    public int OwnerCount { get; set; }
}