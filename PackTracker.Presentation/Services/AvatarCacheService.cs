using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace PackTracker.Presentation.Services;

public sealed class AvatarCacheService
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, BitmapImage?> _cache = new();

    public AvatarCacheService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BitmapImage?> GetAvatarAsync(string? avatarUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return null;
        }

        if (_cache.TryGetValue(avatarUrl, out var cached))
        {
            return cached;
        }

        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(avatarUrl, cancellationToken);

            await using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            _cache[avatarUrl] = image;
            return image;
        }
        catch
        {
            _cache[avatarUrl] = null;
            return null;
        }
    }
}