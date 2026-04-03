using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace PackTracker.Presentation.Converters;

/// <summary name="BoolToVisibilityConverter">
/// Converts a bool to Visibility (true→Visible, false→Collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        (value is bool b && b)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        (value is Visibility v && v == Visibility.Visible);
}