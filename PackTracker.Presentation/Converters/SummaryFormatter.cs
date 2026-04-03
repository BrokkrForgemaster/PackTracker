using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

public sealed class SummaryFormatter : IValueConverter
{
    private static readonly Regex TimestampPattern =
        new(@"\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}.*?\]", RegexOptions.Compiled);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string s) return "";
        // remove the raw timestamp block like [2025-10-08T00:04:52.688Z]
        s = TimestampPattern.Replace(s, "").Trim();
        // collapse double spaces after removal
        s = Regex.Replace(s, @"\s{2,}", " ");
        return s;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}