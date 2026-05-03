using System.Text.Json.Serialization;

namespace PackTracker.Application.Admin.DTOs;

public sealed record ImportMedalsRequestDto(
    [property: JsonPropertyName("available_medals")]
    IReadOnlyList<ImportMedalDefinitionDto> AvailableMedals,

    [property: JsonPropertyName("available_ribbons")]
    IReadOnlyList<ImportMedalDefinitionDto> AvailableRibbons,

    [property: JsonPropertyName("recipients")]
    Dictionary<string, IReadOnlyList<string>> Recipients)
{
    public ImportMedalsRequestDto()
        : this(
            [],
            [],
            new Dictionary<string, IReadOnlyList<string>>())
    {
    }
}

public sealed record ImportMedalDefinitionDto(
    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("description")]
    string Description,

    [property: JsonPropertyName("image")]
    string? Image);