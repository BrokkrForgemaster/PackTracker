namespace PackTracker.Application.DTOS.Uex;

/// <summary>
/// Represents a trade route returned by UEX.
/// </summary>
public class UexTradeRouteDto
{
    public int Id { get; set; }
    public int IdCommodity { get; set; }
    public int IdTerminalOrigin { get; set; }
    public int IdTerminalDestination { get; set; }
    public string CommodityName { get; set; } = default!;
    public string CommodityCode { get; set; } = default!;

    public string OriginTerminalName { get; set; } = default!;
    public string DestinationTerminalName { get; set; } = default!;

    public float PriceOrigin { get; set; }
    public float PriceDestination { get; set; }
    public float PriceMargin { get; set; }
    public float PriceRoi { get; set; }

    public float Investment { get; set; }
    public float Profit { get; set; }
    public float Distance { get; set; }

    public int Score { get; set; }
}