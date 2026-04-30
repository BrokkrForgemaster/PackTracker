using System.Text.Json.Serialization;

namespace PackTracker.Application.Admin.DTOs;

public sealed record ImportMedalsRequestDto(
    [property: JsonPropertyName("available_medals")] IReadOnlyList<ImportMedalDefinitionDto> AvailableMedals,
    [property: JsonPropertyName("recipients")] IReadOnlyDictionary<string, IReadOnlyList<string>> Recipients);

public sealed record ImportMedalDefinitionDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("image")] string? Image);
