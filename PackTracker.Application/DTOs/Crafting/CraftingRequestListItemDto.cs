namespace PackTracker.Application.DTOs.Crafting;

public sealed class CraftingRequestListItemDto
{
    public Guid Id { get; set; }
    public Guid BlueprintId { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public string CraftedItemName { get; set; } = string.Empty;
    public string RequesterUsername { get; set; } = string.Empty;
    public string? AssignedCrafterUsername { get; set; }
    public int QuantityRequested { get; set; }
    public int MinimumQuality { get; set; }
    public string? RefusalReason { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DeliveryLocation { get; set; }
    public string? RewardOffered { get; set; }
    public DateTime? RequiredBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<BlueprintRecipeMaterialDto> Materials { get; set; } = Array.Empty<BlueprintRecipeMaterialDto>();
}
