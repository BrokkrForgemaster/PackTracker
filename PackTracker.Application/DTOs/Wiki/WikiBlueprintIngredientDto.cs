namespace PackTracker.Application.DTOs.Wiki;

public class WikiBlueprintIngredientDto
{
    public string? Uuid { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Type { get; set; }
}
