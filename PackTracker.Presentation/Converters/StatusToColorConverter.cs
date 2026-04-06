using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PackTracker.Presentation.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            "Complete" => Brushes.LimeGreen,
            "Processing" => Brushes.Orange,
            "Pending" => Brushes.Gold,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}