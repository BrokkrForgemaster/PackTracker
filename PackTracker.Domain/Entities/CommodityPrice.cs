namespace PackTracker.Domain.Entities;

/// <summary name="CommodityPrice">
/// Represents the price information of a commodity at a specific terminal.
/// </summary>
public class CommodityPrice
{
    public int Id { get; set; }
    public int CommodityId { get; set; }
    public Commodity Commodity { get; set; } = default!;

    public int TerminalId { get; set; }
    public string TerminalName { get; set; } = default!;
    public string TerminalCode { get; set; } = default!;
    public string TerminalSlug { get; set; } = default!;

    public float PriceBuy { get; set; }
    public float PriceBuyAvg { get; set; }
    public float PriceSell { get; set; }
    public float PriceSellAvg { get; set; }

    public float ScuBuy { get; set; }
    public float ScuBuyAvg { get; set; }
    public float ScuSellStock { get; set; }
    public float ScuSellStockAvg { get; set; }
    public float ScuSell { get; set; }
    public float ScuSellAvg { get; set; }

    public int? StatusBuy { get; set; }
    public int? StatusSell { get; set; }

    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }
    
}