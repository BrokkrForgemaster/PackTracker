using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.Services;

public sealed class AuthTokenService
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);

    private readonly ISettingsService _settingsService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthTokenService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthTokenService(
        ISettingsService settingsService,
        IHttpClientFactory httpClientFactory,
        ILogger<AuthTokenService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.GetSettings();
        var token = settings.JwtToken;

        if (!NeedsRefresh(token))
            return token;

        if (string.IsNullOrWhiteSpace(settings.JwtRefreshToken))
            return token;

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            settings = _settingsService.GetSettings();
            token = settings.JwtToken;
            if (!NeedsRefresh(token))
                return token;

            var refreshed = await RefreshAsync(
                settings.ApiBaseUrl,
                settings.JwtRefreshToken!,
                cancellationToken).ConfigureAwait(false);

            if (refreshed is null)
                return token;

            await _settingsService.UpdateSettingsAsync(s =>
            {
                s.JwtToken = refreshed.access_token;
                s.JwtRefreshToken = refreshed.refresh_token;
            }).ConfigureAwait(false);

            return refreshed.access_token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<TokenPayload?> RefreshAsync(
        string apiBaseUrl,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            return null;

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(AuthTokenService));
            client.BaseAddress = new Uri($"{apiBaseUrl.TrimEnd('/')}/");

            using var response = await client.PostAsJsonAsync(
                "api/v1/auth/refresh",
                new RefreshRequest(refreshToken),
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "JWT refresh failed with status code {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<TokenPayload>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT refresh request failed.");
            return null;
        }
    }

    private static bool NeedsRefresh(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo <= DateTime.UtcNow.Add(RefreshSkew);
        }
        catch
        {
            return true;
        }
    }

    private sealed record RefreshRequest([property: JsonPropertyName("refreshToken")] string RefreshToken);

    public sealed record TokenPayload(string access_token, string refresh_token, int expires_in);
}
