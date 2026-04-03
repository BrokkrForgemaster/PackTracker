namespace PackTracker.Application.DTOs.Uex
{
    public class UexCommodityPriceResponse
    {
        public string Status { get; set; } = default!;
        public int Http_Code { get; set; }
        public List<CommodityPriceDto> Data { get; set; } = new();
    }

    /// <summary>
    /// DTO for GET /2.0/commodities_prices_all
    /// Matches UEX fields exactly (case + underscores) so System.Text.Json can bind cleanly.
    /// </summary>
    public class CommodityPriceDto
    {
        public int Id { get; set; }
        public int Id_Commodity { get; set; }
        public int Id_Terminal { get; set; }

        // Prices (last + avg)
        public float Price_Buy { get; set; }
        public float Price_Buy_Avg { get; set; }
        public float Price_Sell { get; set; }
        public float Price_Sell_Avg { get; set; }

        // SCU (last + avg)
        public float Scu_Buy { get; set; }
        public float Scu_Buy_Avg { get; set; }
        public float Scu_Sell_Stock { get; set; }
        public float Scu_Sell_Stock_Avg { get; set; }
        public float Scu_Sell { get; set; }
        public float Scu_Sell_Avg { get; set; }

        // Inventory states (nullable per API)
        public int? Status_Buy { get; set; }
        public int? Status_Sell { get; set; }

        // Container sizes CSV like "1|2|4|8|16|..."
        public string? Container_Sizes { get; set; }

        // Timestamps (unix seconds)
        public long Date_Added { get; set; }
        public long Date_Modified { get; set; }

        // Commodity identity
        public string Commodity_Name { get; set; } = default!;
        public string? Commodity_Code { get; set; }  // <-- added
        public string? Commodity_Slug { get; set; }  // <-- added

        // Terminal identity
        public string Terminal_Name { get; set; } = default!;
        public string? Terminal_Code { get; set; }   // <-- added
        public string? Terminal_Slug { get; set; }   // <-- added
    }
}