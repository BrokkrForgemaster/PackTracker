using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PackTracker.Presentation.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        // Simple logic: if object is null, hide it.
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) 
        => throw new NotImplementedException();
}