using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PackTracker.Presentation.Converters;

public sealed class ImagePathToPackUriConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path)) return null;

        var normalized = NormalizePath(path);
        return Cache.GetOrAdd(normalized, CreateImageSource);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("../", StringComparison.Ordinal) || normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[(normalized.IndexOf('/', StringComparison.Ordinal) + 1)..];
        }

        return normalized;
    }

    internal static BitmapSource? CreateImageSource(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/{normalizedPath}", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();

            var trimmed = TrimTransparentBounds(bitmap);
            if (trimmed is not null && trimmed.CanFreeze)
            {
                trimmed.Freeze();
            }

            return trimmed;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to load image resource {Path}", normalizedPath);
            return null;
        }
    }

    internal static BitmapSource TrimTransparentBounds(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            return source;
        }

        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        var minX = converted.PixelWidth;
        var minY = converted.PixelHeight;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < converted.PixelHeight; y++)
        {
            for (var x = 0; x < converted.PixelWidth; x++)
            {
                var alpha = pixels[(y * stride) + (x * 4) + 3];
                if (alpha == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return source;
        }

        if (minX == 0
            && minY == 0
            && maxX == converted.PixelWidth - 1
            && maxY == converted.PixelHeight - 1)
        {
            return source;
        }

        return new CroppedBitmap(
            source,
            new Int32Rect(
                minX,
                minY,
                maxX - minX + 1,
                maxY - minY + 1));
    }
}
