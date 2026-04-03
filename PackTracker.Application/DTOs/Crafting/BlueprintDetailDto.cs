namespace PackTracker.Application.DTOs.Crafting;

public sealed class BlueprintDetailDto
{
    public Guid Id { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public string CraftedItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsInGameAvailable { get; set; }
    public string? AcquisitionSummary { get; set; }
    public string? AcquisitionLocation { get; set; }
    public string? AcquisitionMethod { get; set; }
    public string? SourceVersion { get; set; }
    public string DataConfidence { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int OutputQuantity { get; set; }
    public string? CraftingStationType { get; set; }
    public int? TimeToCraftSeconds { get; set; }
    public IReadOnlyList<BlueprintRecipeMaterialDto> Materials { get; set; } = Array.Empty<BlueprintRecipeMaterialDto>();
    public IReadOnlyList<BlueprintOwnerDto> Owners { get; set; } = Array.Empty<BlueprintOwnerDto>();
}
