namespace PackTracker.Application.Options;

public class RegolithOptions
{
    public string BaseUrl { get; set; } = "https://api.regolith.rocks";
    public string ApiKey { get; set; } = string.Empty;
    public bool UseStub { get; set; }
}