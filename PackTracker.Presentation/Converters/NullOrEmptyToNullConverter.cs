using System.Globalization;
using System.Windows.Data;

namespace PackTracker.Presentation.Converters;

/// <summary>
/// Returns the value as-is if non-empty, or null if the string is null/empty/whitespace.
/// Used to suppress tooltips when there is no content.
/// </summary>
public sealed class NullOrEmptyToNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value;
}
