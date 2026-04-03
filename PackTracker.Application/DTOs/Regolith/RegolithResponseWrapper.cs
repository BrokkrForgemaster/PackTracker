using System.Text.Json.Serialization;

namespace PackTracker.Application.DTOs.Regolith;

public class RegolithProfileResponse
{
    [JsonPropertyName("data")]
    public RegolithProfileData Data { get; set; } = new();
}

public class RegolithProfileData
{
    [JsonPropertyName("profile")]
    public RegolithProfileDtoRaw? Profile { get; set; }
}

public class RegolithRefineryJobsResponse
{
    [JsonPropertyName("data")]
    public RegolithRefineryJobsData Data { get; set; } = new();
}

public class RegolithRefineryJobsData
{
    [JsonPropertyName("refineryJobs")]
    public List<RegolithRefineryJobDtoRaw> RefineryJobs { get; set; } = new();
}
