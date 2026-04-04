using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PackTracker.Presentation.Converters
{
    public class BooleanToStatusBrushConverter : IValueConverter {
        public object Convert(object v, Type t, object p, CultureInfo c) => 
            (bool)v ? new SolidColorBrush(Color.FromRgb(255, 215, 0)) : new SolidColorBrush(Colors.Transparent);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
