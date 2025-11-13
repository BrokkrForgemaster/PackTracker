using System;
using System.Globalization;
using System.Windows.Data;
using PackTracker.Domain.Enums;

namespace PackTracker.Presentation.Converters;

public class RequestKindToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RequestKind kind)
        {
            return kind switch
            {
                RequestKind.MiningMaterials => "⛏️",
                RequestKind.TradingGoods => "📦",
                RequestKind.ShipComponents => "🔧",
                RequestKind.MissionBackup => "🎯",
                RequestKind.CargoEscort => "🛡️",
                RequestKind.CombatSupport => "⚔️",
                RequestKind.ShipCrew => "👥",
                RequestKind.Transportation => "🚀",
                RequestKind.LocationScout => "🔍",
                RequestKind.Guidance => "📚",
                RequestKind.EventSupport => "🎪",
                _ => "❓"
            };
        }
        return "❓";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
