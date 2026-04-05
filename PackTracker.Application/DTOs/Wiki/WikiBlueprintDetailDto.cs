namespace PackTracker.Application.DTOs.Wiki;

public class WikiBlueprintDetailDto
{
    public string Uuid { get; set; } = string.Empty;
    public string? Key { get; set; }
    public string? CategoryUuid { get; set; }
    public string? OutputItemUuid { get; set; }
    public string? OutputName { get; set; }
    public string? OutputClass { get; set; }
    public WikiBlueprintOutputDto? Output { get; set; }
    public int CraftTimeSeconds { get; set; }
    public bool IsAvailableByDefault { get; set; }
    public string? GameVersion { get; set; }
    public int IngredientCount { get; set; }
    public List<WikiBlueprintIngredientDto> Ingredients { get; set; } = new();
    public WikiBlueprintAvailabilityDto? Availability { get; set; }
    public List<WikiBlueprintRequirementGroupDto> RequirementGroups { get; set; } = new();
}

public class WikiBlueprintAvailabilityDto
{
    public bool Default { get; set; }
    public List<WikiRewardPoolDto> RewardPools { get; set; } = new();
}

public class WikiRewardPoolDto
{
    public string? Key { get; set; }
    public string? Uuid { get; set; }
}

public class WikiBlueprintRequirementGroupDto
{
    public string? Key { get; set; }
    public string? Name { get; set; }
    public string? Kind { get; set; }
    public int RequiredCount { get; set; }
    public List<WikiBlueprintModifierDto> Modifiers { get; set; } = new();
    public List<WikiBlueprintRequirementChildDto> Children { get; set; } = new();
}

public class WikiBlueprintModifierDto
{
    public string? PropertyKey { get; set; }
    public string? Label { get; set; }
    public string? BetterWhen { get; set; }
    public WikiModifierRangeDto? ModifierRange { get; set; }
}

public class WikiModifierRangeDto
{
    public double AtMinQuality { get; set; }
    public double AtMaxQuality { get; set; }
}

public class WikiBlueprintRequirementChildDto
{
    public string? Key { get; set; }
    public string? Kind { get; set; }
    public string? Uuid { get; set; }
    public string? Name { get; set; }
    public int? RequiredCount { get; set; }
    public double? Quantity { get; set; }
    public double? QuantityScu { get; set; }
    public int MinQuality { get; set; }
    public List<WikiBlueprintModifierDto> Modifiers { get; set; } = new();
}
