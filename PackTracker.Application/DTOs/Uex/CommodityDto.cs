namespace PackTracker.Application.DTOs.Uex;

public class UexCommodityResponse
{
    public string Status { get; set; } = default!;
    public int Http_Code { get; set; }
    public List<CommodityDto> Data { get; set; } = new();
}

public class CommodityDto
{
    public int Id { get; set; }
    public int? Id_Parent { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public double? Weight_Scu { get; set; }

    public float? Price_Buy { get; set; }
    public float? Price_Sell { get; set; }

    // boolean-ish ints (UEX returns 0/1)
    public int Is_Available { get; set; }
    public int Is_Available_Live { get; set; }
    public int Is_Visible { get; set; }
    public int Is_Extractable { get; set; }
    public int Is_Mineral { get; set; }
    public int Is_Raw { get; set; }
    public int Is_Pure { get; set; }
    public int Is_Refined { get; set; }
    public int Is_Refinable { get; set; }
    public int Is_Harvestable { get; set; }
    public int Is_Buyable { get; set; }
    public int Is_Sellable { get; set; }
    public int Is_Temporary { get; set; }
    public int Is_Illegal { get; set; }
    public int Is_Volatile_Qt { get; set; }
    public int Is_Volatile_Time { get; set; }
    public int Is_Inert { get; set; }
    public int Is_Explosive { get; set; }
    public int Is_Fuel { get; set; }
    public int Is_Buggy { get; set; }

    public string? Wiki { get; set; }

    public long Date_Added { get; set; }
    public long Date_Modified { get; set; }
}