namespace PackTracker.Domain.Entities;

public class BlueprintRecipeMaterial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlueprintRecipeId { get; set; }
    public Guid MaterialId { get; set; }
    public decimal QuantityRequired { get; set; }
    public string Unit { get; set; } = "SCU";
    public bool IsOptional { get; set; }
    public bool IsIntermediateCraftable { get; set; }
    public string? Notes { get; set; }

    public BlueprintRecipe? BlueprintRecipe { get; set; }
    public Material? Material { get; set; }
}
