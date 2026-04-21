namespace PackTracker.Domain.Entities;

/// <summary name="Commodity">
/// Represents a commodity in the system.
/// </summary>
public class Commodity
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Kind { get; set; }
    public int? WeightScu { get; set; }
    
    public decimal? ROI { get; set; }
    public decimal? PriceBuy { get; set; }
    public decimal? PriceSell { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsAvailableLive { get; set; }
    public bool IsVisible { get; set; }
    public bool IsExtractable { get; set; }
    public bool IsMineral { get; set; }
    public bool IsRaw { get; set; }
    public bool IsPure { get; set; }
    public bool IsRefined { get; set; }
    public bool IsRefinable { get; set; }
    public bool IsHarvestable { get; set; }
    public bool IsBuyable { get; set; }
    public bool IsSellable { get; set; }
    public bool IsTemporary { get; set; }
    public bool IsIllegal { get; set; }
    public bool IsVolatileQt { get; set; }
    public bool IsVolatileTime { get; set; }
    public bool IsInert { get; set; }
    public bool IsExplosive { get; set; }
    public bool IsBuggy { get; set; }
    public bool IsFuel { get; set; }
    public string? Wiki { get; set; }
    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }

    public ICollection<CommodityPrice> Prices { get; set; } = new List<CommodityPrice>();
}