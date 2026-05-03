using System.Globalization;
using System.Windows.Data;

namespace PackTracker.Presentation.Converters;

public sealed class ImagePathToPackUriConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path)) return null;
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("../") || normalized.StartsWith("./"))
            normalized = normalized[(normalized.IndexOf('/') + 1)..];
        return $"pack://application:,,,/{normalized}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
