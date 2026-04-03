namespace PackTracker.Domain.Entities;

public class BlueprintRecipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlueprintId { get; set; }
    public int OutputQuantity { get; set; } = 1;
    public string? CraftingStationType { get; set; }
    public int? TimeToCraftSeconds { get; set; }
    public string? Notes { get; set; }

    public Blueprint? Blueprint { get; set; }
}
