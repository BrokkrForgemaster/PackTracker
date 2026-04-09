using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PackTracker.Presentation.Converters;

/// <summary>
/// Maps a quality value (0–1000) to a SolidColorBrush.
/// Below 500: dark red (0) → bright red (499)
/// Above 500: bright green (501) → dark green (1000)
/// </summary>
[ValueConversion(typeof(double), typeof(SolidColorBrush))]
public class QualityColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double quality = value is double d ? d : 0;
        double t = Math.Clamp(quality / 1000.0, 0.0, 1.0);

        Color color;
        if (t <= 0.5)
        {
            // dark red (t=0) → bright red (t=0.5)
            double u = t / 0.5;
            color = Lerp(Color.FromRgb(0x70, 0x00, 0x00), Color.FromRgb(0xFF, 0x22, 0x22), u);
        }
        else
        {
            // bright green (t=0.5) → dark green (t=1.0)
            double u = (t - 0.5) / 0.5;
            color = Lerp(Color.FromRgb(0x22, 0xFF, 0x44), Color.FromRgb(0x00, 0x48, 0x18), u);
        }

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));
}
