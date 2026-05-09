namespace PackTracker.Mobile.Services;

public interface ITokenStorage
{
    Task<string?> GetTokenAsync(string key);
    Task SetTokenAsync(string key, string value);
    Task RemoveTokenAsync(string key);
}
