using System;
using System.Globalization;
using System.Windows.Data;

namespace PackTracker.Presentation.Converters
{
    /// <summary>
    /// Converts a KillEntity.Type into an emoji or short text icon.
    /// </summary>
    public sealed class KillTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value?.ToString()?.Trim().ToLowerInvariant() ?? "";

            return type switch
            {
                "fps" or "infantry" or "ground" => "🪖",
                "air" or "fighter" or "bomber" => "✈️",
                "ship" or "capital" => "🚀",
                "turret" or "aa" => "🎯",
                "vehicle" or "ground_vehicle" => "🚙",
                _ => "💥"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}