using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PackTracker.Mobile.Services;

public sealed class MobileAuthService
{
    private readonly MobileSessionService _session;

    public MobileAuthService(MobileSessionService session)
    {
        _session = session;
    }

    public string ApiBaseUrl => _session.GetApiBaseUrl();

    public async Task<bool> WaitForApiAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        var readinessUrl = $"{ApiBaseUrl}/health/ready";
        var livenessUrl = $"{ApiBaseUrl}/health";

        for (var attempt = 0; attempt < 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var readiness = await client.GetAsync(readinessUrl, cancellationToken).ConfigureAwait(false);
                if (readiness.IsSuccessStatusCode)
                    return true;

                if (readiness.StatusCode == HttpStatusCode.NotFound)
                {
                    var liveness = await client.GetAsync(livenessUrl, cancellationToken).ConfigureAwait(false);
                    if (liveness.IsSuccessStatusCode)
                        return true;
                }
            }
            catch
            {
            }

            await Task.Delay(750, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<string> StartLoginAsync(CancellationToken cancellationToken = default)
    {
        var clientState = Guid.NewGuid().ToString("N");
        var loginUrl = $"{ApiBaseUrl}/api/v1/auth/login?clientState={clientState}";

        await Browser.Default.OpenAsync(loginUrl, BrowserLaunchMode.SystemPreferred).ConfigureAwait(false);
        return clientState;
    }

    public async Task<bool> PollLoginAsync(string clientState, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"{ApiBaseUrl}/")
        };

        for (var attempt = 0; attempt < 90; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await client.GetAsync(
                    $"api/v1/auth/poll/{clientState}",
                    cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<LoginTokenPayload>(
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (payload is not null)
                    {
                        await _session.SetTokensAsync(payload.access_token, payload.refresh_token).ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<bool> RefreshTokensAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _session.GetRefreshTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"{ApiBaseUrl}/")
        };

        using var response = await client.PostAsJsonAsync(
            "api/v1/auth/refresh",
            new RefreshTokenRequest(refreshToken),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return false;

        var payload = await response.Content.ReadFromJsonAsync<LoginTokenPayload>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (payload is null)
            return false;

        await _session.SetTokensAsync(payload.access_token, payload.refresh_token).ConfigureAwait(false);
        return true;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _session.GetRefreshTokenAsync().ConfigureAwait(false);
        var accessToken = await _session.GetAccessTokenAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(refreshToken) && !string.IsNullOrWhiteSpace(accessToken))
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri($"{ApiBaseUrl}/")
            };
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                await client.PostAsJsonAsync(
                    "api/v1/auth/logout",
                    new RefreshTokenRequest(refreshToken),
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        await _session.ClearTokensAsync().ConfigureAwait(false);
    }

    public async Task<bool> IsSignedInAsync()
    {
        var token = await _session.GetAccessTokenAsync().ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(token);
    }

    private sealed record RefreshTokenRequest([property: JsonPropertyName("refreshToken")] string RefreshToken);

    private sealed record LoginTokenPayload(string access_token, string refresh_token, int expires_in);
}
