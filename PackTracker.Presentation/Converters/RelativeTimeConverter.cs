using System;
using System.Globalization;
using System.Windows.Data;

namespace PackTracker.Presentation.Converters
{
    public sealed class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // accept DateTime or string
            DateTime timestamp;

            if (value is DateTime dt)
            {
                timestamp = dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt.ToUniversalTime();
            }
            else if (value is string s)
            {
                // handle formats like 2025-10-08T00:04:52.688Z
                if (!DateTime.TryParse(s, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out timestamp))
                    return s; // fallback: show original if parsing fails
            }
            else return "";

            var local = timestamp.ToLocalTime();
            var diff = DateTime.Now - local;

            if (diff.TotalSeconds < 60)
                return "Just now";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} m ago";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} h ago";
            if (diff.TotalDays < 2)
                return $"Yesterday {local:HH:mm}";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} d ago";
            return local.ToString("MMM dd HH:mm", CultureInfo.CurrentCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}