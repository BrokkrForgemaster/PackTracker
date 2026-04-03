using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PackTracker.Domain.Enums;

namespace PackTracker.Presentation.Converters;

public class RequestKindToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RequestKind kind)
        {
            return kind switch
            {
                RequestKind.MiningMaterials => new SolidColorBrush(Color.FromRgb(196, 122, 42)), // Orange/brown
                RequestKind.TradingGoods => new SolidColorBrush(Color.FromRgb(67, 110, 230)), // Blue
                RequestKind.ShipComponents => new SolidColorBrush(Color.FromRgb(156, 89, 209)), // Purple
                RequestKind.MissionBackup => new SolidColorBrush(Color.FromRgb(229, 83, 83)), // Red
                RequestKind.CargoEscort => new SolidColorBrush(Color.FromRgb(229, 162, 42)), // Gold
                RequestKind.CombatSupport => new SolidColorBrush(Color.FromRgb(194, 24, 24)), // Dark red
                RequestKind.ShipCrew => new SolidColorBrush(Color.FromRgb(45, 131, 127)), // Teal
                RequestKind.Transportation => new SolidColorBrush(Color.FromRgb(114, 137, 218)), // Light blue
                RequestKind.LocationScout => new SolidColorBrush(Color.FromRgb(88, 178, 92)), // Green
                RequestKind.Guidance => new SolidColorBrush(Color.FromRgb(194, 162, 58)), // Yellow
                RequestKind.EventSupport => new SolidColorBrush(Color.FromRgb(153, 102, 204)), // Lavender
                _ => new SolidColorBrush(Color.FromRgb(119, 119, 119)) // Gray
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
