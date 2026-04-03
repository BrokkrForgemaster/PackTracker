namespace PackTracker.Application.DTOs.Crafting;

public sealed class MaterialProcurementRequestListItemDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public Guid? LinkedCraftingRequestId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal QuantityRequested { get; set; }
    public decimal QuantityDelivered { get; set; }
    public string PreferredForm { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DeliveryLocation { get; set; }
    public int? NumberOfHelpersNeeded { get; set; }
    public string? RewardOffered { get; set; }
    public DateTime CreatedAt { get; set; }
}
