namespace PackTracker.Application.DTOs.Crafting;

public sealed class OwnedBlueprintSummaryDto
{
    public Guid BlueprintId { get; set; }
    public Guid WikiUuid { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public string CraftedItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AvailabilityStatus { get; set; } = string.Empty;
    public string OwnershipStatus { get; set; } = string.Empty;
    public DateTime? VerifiedAt { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<BlueprintRecipeMaterialDto> Materials { get; set; } = Array.Empty<BlueprintRecipeMaterialDto>();
}
