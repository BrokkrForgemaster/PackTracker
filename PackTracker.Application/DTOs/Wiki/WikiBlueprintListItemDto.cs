namespace PackTracker.Application.DTOs.Wiki;

public class WikiBlueprintListItemDto
{
    public string Uuid { get; set; } = string.Empty;
    public WikiBlueprintOutputDto? Output { get; set; }
    public int CraftTimeSeconds { get; set; }
    public int IngredientCount { get; set; }
    public bool IsAvailableByDefault { get; set; }
}

public class WikiBlueprintOutputDto
{
    public string? Uuid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Class { get; set; }
}
