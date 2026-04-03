namespace PackTracker.Domain.Entities;

public class OrgInventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaterialId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public string? StorageLocation { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Material? Material { get; set; }
}
