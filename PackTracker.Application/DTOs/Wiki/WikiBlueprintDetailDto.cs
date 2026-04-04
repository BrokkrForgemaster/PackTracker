namespace PackTracker.Application.DTOs.Wiki;

public class WikiBlueprintDetailDto
{
    public string Uuid { get; set; } = string.Empty;
    public WikiBlueprintOutputDto? Output { get; set; }
    public int CraftTimeSeconds { get; set; }
    public bool IsAvailableByDefault { get; set; }
    public List<WikiBlueprintIngredientDto> Ingredients { get; set; } = new();
}
