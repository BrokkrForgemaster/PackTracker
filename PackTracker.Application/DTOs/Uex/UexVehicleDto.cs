using System.Text.Json.Serialization;

namespace PackTracker.Application.DTOs.Uex;

public sealed class UexVehicleDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("id_company")] public int IdCompany { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("name_full")] public string? NameFull { get; set; }
    [JsonPropertyName("slug")] public string? Slug { get; set; }
    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
    [JsonPropertyName("scu")] public float? Scu { get; set; }   // cargo capacity
    [JsonPropertyName("crew")] public string? Crew { get; set; }
    [JsonPropertyName("mass")] public float? Mass { get; set; }
    [JsonPropertyName("width")] public float? Width { get; set; }
    [JsonPropertyName("height")] public float? Height { get; set; }
    [JsonPropertyName("length")] public float? Length { get; set; }

    [JsonPropertyName("is_spaceship")] public int IsSpaceship { get; set; }
    [JsonPropertyName("is_ground_vehicle")] public int IsGroundVehicle { get; set; }

    [JsonPropertyName("url_photo")] public string? UrlPhoto { get; set; }
    [JsonPropertyName("company_name")] public string? CompanyName { get; set; }
}