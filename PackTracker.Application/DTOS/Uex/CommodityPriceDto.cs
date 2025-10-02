namespace PackTracker.Application.DTOs.Uex;

public class UexCommodityPriceResponse
{
    public string Status { get; set; } = default!;
    public int Http_Code { get; set; }
    public List<CommodityPriceDto> Data { get; set; } = new();
}

public class CommodityPriceDto
{
    public int Id { get; set; }
    public int Id_Commodity { get; set; }
    public int Id_Terminal { get; set; }

    public float Price_Buy { get; set; }
    public float Price_Buy_Avg { get; set; }
    public float Price_Sell { get; set; }
    public float Price_Sell_Avg { get; set; }

    public float Scu_Buy { get; set; }
    public float Scu_Buy_Avg { get; set; }
    public float Scu_Sell_Stock { get; set; }
    public float Scu_Sell_Stock_Avg { get; set; }
    public float Scu_Sell { get; set; }
    public float Scu_Sell_Avg { get; set; }

    public int? Status_Buy { get; set; }
    public int? Status_Sell { get; set; }

    public string? Container_Sizes { get; set; }

    public long Date_Added { get; set; }
    public long Date_Modified { get; set; }

    public string Commodity_Name { get; set; } = default!;
    public string Terminal_Name { get; set; } = default!;
}