using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace PackTracker.Presentation.Converters;

/// <summary namespace="InverseBoolConverter">
/// Converts a bool to its inverse, optionally mapping to Visibility.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    /// <summary>
    /// Converts a bool to its inverse. If targetType is Visibility, true→Collapsed, false→Visible.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            bool inverted = !b;
            if (targetType == typeof(Visibility))
                return inverted ? Visibility.Visible : Visibility.Collapsed;
            return inverted;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        if (value is Visibility v)
            return v != Visibility.Visible;
        return DependencyProperty.UnsetValue;
    }
}