using System.Net.Http;
using System.Net.Http.Headers;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.Services;

public interface IApiClientProvider
{
    HttpClient CreateClient();
    HttpClient CreateAnonymousClient();
    string BaseUrl { get; }
}

public class ApiClientProvider : IApiClientProvider
{
    private readonly ISettingsService _settingsService;
    private readonly AuthTokenService _authTokenService;

    public ApiClientProvider(ISettingsService settingsService, AuthTokenService authTokenService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _authTokenService = authTokenService ?? throw new ArgumentNullException(nameof(authTokenService));
    }

    public string BaseUrl
    {
        get
        {
            var url = _settingsService.GetSettings().ApiBaseUrl;
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("API base URL is not configured.");
            return url.TrimEnd('/');
        }
    }

    public HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri($"{BaseUrl}/")
        };

        var token = _authTokenService.GetAccessTokenAsync().GetAwaiter().GetResult();
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public HttpClient CreateAnonymousClient() =>
        new HttpClient { BaseAddress = new Uri($"{BaseUrl}/") };
}
