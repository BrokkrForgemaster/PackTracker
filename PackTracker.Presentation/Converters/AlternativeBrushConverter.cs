using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace PackTracker.Presentation.Converters;
/// <summary name="AlternationToBrushConverter">
/// Chooses between BrushA and BrushB based on AlternationIndex (0 or 1).
/// </summary>
public class AlternationToBrushConverter : IValueConverter
{
    public Brush BrushA { get; set; } = Brushes.Transparent;
    public Brush BrushB { get; set; } = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && index == 1)
        {
            return BrushB;
        }
        return BrushA;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}