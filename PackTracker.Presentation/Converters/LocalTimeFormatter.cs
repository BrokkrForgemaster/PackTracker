using System;
using System.Globalization;
using System.Windows.Data;

namespace PackTracker.Presentation.Converters
{
    public sealed class LocalTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
                return dt.ToLocalTime().ToString("h:mm tt", CultureInfo.CurrentCulture); // 10:04 PM
            if (value is string s && DateTime.TryParse(s, null,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                return parsed.ToLocalTime().ToString("h:mm tt", CultureInfo.CurrentCulture);

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}