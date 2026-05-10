namespace PackTracker.Mobile.Services;

public sealed class MobileSessionService
{
    public const string DefaultApiBaseUrl = "https://packtracker-yke3.onrender.com";

    private const string ApiBaseUrlKey = "api_base_url";
    private const string AccessTokenKey = "jwt_access_token";
    private const string RefreshTokenKey = "jwt_refresh_token";

    private readonly ITokenStorage _tokenStorage;

    public MobileSessionService(ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    public string GetApiBaseUrl()
    {
        var configured = Preferences.Default.Get(ApiBaseUrlKey, DefaultApiBaseUrl);
        return string.IsNullOrWhiteSpace(configured)
            ? DefaultApiBaseUrl
            : configured.TrimEnd('/');
    }

    public void SetApiBaseUrl(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? DefaultApiBaseUrl
            : value.Trim().TrimEnd('/');
        Preferences.Default.Set(ApiBaseUrlKey, normalized);
    }

    public Task<string?> GetAccessTokenAsync() => _tokenStorage.GetTokenAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync() => _tokenStorage.GetTokenAsync(RefreshTokenKey);

    public Task SetTokensAsync(string accessToken, string refreshToken)
    {
        return Task.WhenAll(
            _tokenStorage.SetTokenAsync(AccessTokenKey, accessToken),
            _tokenStorage.SetTokenAsync(RefreshTokenKey, refreshToken));
    }

    public Task ClearTokensAsync()
    {
        return Task.WhenAll(
            _tokenStorage.RemoveTokenAsync(AccessTokenKey),
            _tokenStorage.RemoveTokenAsync(RefreshTokenKey));
    }
}
