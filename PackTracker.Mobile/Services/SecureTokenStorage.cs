namespace PackTracker.Mobile.Services;

public sealed class SecureTokenStorage : ITokenStorage
{
    public async Task<string?> GetTokenAsync(string key) =>
        await SecureStorage.Default.GetAsync(key).ConfigureAwait(false);

    public async Task SetTokenAsync(string key, string value) =>
        await SecureStorage.Default.SetAsync(key, value).ConfigureAwait(false);

    public Task RemoveTokenAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}
