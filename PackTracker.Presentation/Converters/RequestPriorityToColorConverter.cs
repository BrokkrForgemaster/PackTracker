using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PackTracker.Domain.Enums;

namespace PackTracker.Presentation.Converters;

public class RequestPriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RequestPriority priority)
        {
            return priority switch
            {
                RequestPriority.Critical => new SolidColorBrush(Color.FromRgb(220, 53, 69)), // Bright red
                RequestPriority.High => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Bright yellow
                RequestPriority.Normal => new SolidColorBrush(Color.FromRgb(25, 135, 84)), // Green
                RequestPriority.Low => new SolidColorBrush(Color.FromRgb(108, 117, 125)), // Gray
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
