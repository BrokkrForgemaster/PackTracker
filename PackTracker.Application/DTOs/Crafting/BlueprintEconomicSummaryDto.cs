namespace PackTracker.Application.DTOs.Crafting;

public sealed class BlueprintEconomicSummaryDto
{
    public Guid BlueprintId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    
    /// <summary>
    /// The sum of the average market prices for all required materials.
    /// </summary>
    public decimal TotalCraftingCost { get; set; }
    
    /// <summary>
    /// The average selling price of the finished item on the market (if available).
    /// </summary>
    public decimal? EstimatedMarketValue { get; set; }
    
    /// <summary>
    /// Comparison: Market Value - Crafting Cost.
    /// </summary>
    public decimal? PotentialProfit => EstimatedMarketValue.HasValue 
        ? EstimatedMarketValue.Value - TotalCraftingCost 
        : null;

    public List<MaterialCostDto> Materials { get; set; } = new();
}

public sealed class MaterialCostDto
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public double QuantityRequired { get; set; }
    public string Unit { get; set; } = "Units";
    
    /// <summary>
    /// Current average buy price from UEX data.
    /// </summary>
    public decimal AvgPricePerUnit { get; set; }
    
    public decimal TotalCost => (decimal)QuantityRequired * AvgPricePerUnit;
}
