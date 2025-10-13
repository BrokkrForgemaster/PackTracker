using System.Text.Json.Serialization;

public sealed class UexTradeRouteDto
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("id_commodity")] public int IdCommodity { get; set; }

    [JsonPropertyName("origin_terminal_name")] public string? OriginTerminalName { get; set; }
    [JsonPropertyName("destination_terminal_name")] public string? DestinationTerminalName { get; set; }

    [JsonPropertyName("price_origin")] public decimal? PriceOrigin { get; set; }
    [JsonPropertyName("price_destination")] public decimal? PriceDestination { get; set; }

    [JsonPropertyName("price_margin")] public decimal? PriceMargin { get; set; }
    [JsonPropertyName("price_roi")] public decimal? PriceRoi { get; set; }

    [JsonPropertyName("profit")] public decimal? Profit { get; set; }
    [JsonPropertyName("distance")] public decimal? Distance { get; set; }

    [JsonPropertyName("commodity_name")] public string? CommodityName { get; set; }
}