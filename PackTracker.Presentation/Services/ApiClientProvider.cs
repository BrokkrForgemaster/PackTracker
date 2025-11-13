using System.Net.Http;
using System.Net.Http.Headers;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.Services;

public interface IApiClientProvider
{
    HttpClient CreateClient();
    string BaseUrl { get; }
}

public class ApiClientProvider : IApiClientProvider
{
    private readonly ISettingsService _settingsService;

    public ApiClientProvider(ISettingsService settingsService) =>
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

    public string BaseUrl
    {
        get
        {
            var url = _settingsService.GetSettings().ApiBaseUrl ?? "http://localhost:5001";
            return url.TrimEnd('/');
        }
    }

    public HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri($"{BaseUrl}/")
        };

        var token = _settingsService.GetSettings().JwtToken;
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
