using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace PackTracker.Presentation.Converters;

/// <summary>
/// Converts a PascalCase enum value (or null) to a spaced display string.
/// e.g. MiningMaterials → "Mining Materials", null → "All"
/// </summary>
public class EnumToDisplayNameConverter : IValueConverter
{
    public string NullLabel { get; set; } = "All";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return NullLabel;
        var name = value.ToString() ?? string.Empty;
        return Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
