namespace PackTracker.Application.DTOs.Crafting;

public sealed class BlueprintRecipeMaterialDto
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public bool IsIntermediateCraftable { get; set; }
    public string SourceType { get; set; } = string.Empty;
}
