using System.Windows;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.Services;

/// <summary name="ThemeManager">
/// Manages the application themes by applying the selected theme to the application's resources.
/// </summary>
public class ThemeManager : IThemeManager
{
    private const string ThemeFolder = "Themes/";
    private readonly System.Windows.Application _app;
    public string[] AvailableThemes { get; }
    public string CurrentTheme { get; private set; }

    public ThemeManager(System.Windows.Application application)
    {
        _app = application ?? throw new ArgumentNullException(nameof(application));
        AvailableThemes = new[] { "Dark", "Locops", "Tacops", "Specops" };
        CurrentTheme = AvailableThemes[0];
        ApplyTheme(CurrentTheme);
    }

    public void ApplyTheme(string themeName)
    {
        if (!AvailableThemes.Contains(themeName, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unknown theme: {themeName}", nameof(themeName));

        var toRemove = _app.Resources.MergedDictionaries
            .Where(d => d.Source != null &&
                        d.Source.OriginalString.IndexOf("/Themes/", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        foreach (var dic in toRemove)
            _app.Resources.MergedDictionaries.Remove(dic);

        var uri = new Uri($"/PackTracker.Presentation;component/Themes/{themeName}.xaml", UriKind.Relative);
        var themeDict = new ResourceDictionary { Source = uri };
        _app.Resources.MergedDictionaries.Add(themeDict);

        CurrentTheme = themeName;
    }
}