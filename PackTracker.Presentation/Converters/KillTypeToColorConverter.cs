using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace PackTracker.Presentation.Converters;

public class KillTypeToColorConverter : IValueConverter
{
    /// <summary name="KillTypeToColorConverter">
    /// Initializes a new instance of the <see cref="KillTypeToColorConverter"/> class
    /// Converts a kill Type ("FPS", "Air", "Info") into a SolidColorBrush.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string type)
            return new SolidColorBrush(Color.FromRgb(80, 80, 80));

        return type switch
        {
            "FPS" => new SolidColorBrush(Color.FromRgb(34, 139, 34)),
            "Air" => new SolidColorBrush(Color.FromRgb(178, 34, 34)),
            "Info" => new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            _ => new SolidColorBrush(Color.FromRgb(80, 80, 80)),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}