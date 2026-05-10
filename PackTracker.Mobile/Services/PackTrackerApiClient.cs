using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PackTracker.Mobile.Services;

public sealed class PackTrackerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly MobileSessionService _session;
    private readonly MobileAuthService _auth;

    public PackTrackerApiClient(MobileSessionService session, MobileAuthService auth)
    {
        _session = session;
        _auth = auth;
    }

    public async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync<object?>(HttpMethod.Get, relativeUrl, null, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return default;

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task<HttpResponseMessage> PostAsync<T>(string relativeUrl, T payload, CancellationToken cancellationToken = default)
    {
        return SendAsync(HttpMethod.Post, relativeUrl, payload, cancellationToken);
    }

    public Task<HttpResponseMessage> PutAsync<T>(string relativeUrl, T payload, CancellationToken cancellationToken = default)
    {
        return SendAsync(HttpMethod.Put, relativeUrl, payload, cancellationToken);
    }

    public Task<HttpResponseMessage> PatchAsync(string relativeUrl, CancellationToken cancellationToken = default)
    {
        return SendAsync<object?>(HttpMethod.Patch, relativeUrl, null, cancellationToken);
    }

    public Task<HttpResponseMessage> DeleteAsync(string relativeUrl, CancellationToken cancellationToken = default)
    {
        return SendAsync<object?>(HttpMethod.Delete, relativeUrl, null, cancellationToken);
    }

    public async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
            return $"HTTP {(int)response.StatusCode}";

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("message", out var message))
                return message.GetString() ?? $"HTTP {(int)response.StatusCode}";
            if (doc.RootElement.TryGetProperty("error", out var error))
                return error.GetString() ?? $"HTTP {(int)response.StatusCode}";
        }
        catch
        {
        }

        return content;
    }

    private async Task<HttpResponseMessage> SendAsync<T>(
        HttpMethod method,
        string relativeUrl,
        T? payload,
        CancellationToken cancellationToken)
    {
        var response = await SendCoreAsync(method, relativeUrl, payload, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        var refreshed = await _auth.RefreshTokensAsync(cancellationToken).ConfigureAwait(false);
        return refreshed
            ? await SendCoreAsync(method, relativeUrl, payload, cancellationToken).ConfigureAwait(false)
            : new HttpResponseMessage(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> SendCoreAsync<T>(
        HttpMethod method,
        string relativeUrl,
        T? payload,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"{_session.GetApiBaseUrl()}/")
        };

        var accessToken = await _session.GetAccessTokenAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var request = new HttpRequestMessage(method, relativeUrl);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }
        else if (method == HttpMethod.Patch)
        {
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
