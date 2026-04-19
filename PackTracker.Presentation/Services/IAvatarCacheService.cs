using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PackTracker.Presentation.Services;

public interface IAvatarCacheService
{
    Task<BitmapImage?> GetAvatarAsync(string url, CancellationToken cancellationToken = default);
}