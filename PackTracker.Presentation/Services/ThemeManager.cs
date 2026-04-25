using System.Windows;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.Services;

/// <summary name="ThemeManager">
/// Manages the application themes by applying the selected theme to the application's resources.
/// </summary>
public class ThemeManager : IThemeManager
{
    private const string ThemeFolder = "Themes/";
    private static readonly Uri BaseThemeUri = new($"/PackTracker.Presentation;component/{ThemeFolder}Dark.xaml", UriKind.Relative);

    private readonly System.Windows.Application _app;
    public string[] AvailableThemes { get; }
    public string CurrentTheme { get; private set; }

    public ThemeManager(System.Windows.Application application)
    {
        _app = application ?? throw new ArgumentNullException(nameof(application));
        AvailableThemes = new[] { "Dark", "Locops", "Tacops", "Specops", "Arcops" };
        CurrentTheme = AvailableThemes[0];
        ApplyTheme(CurrentTheme);
    }

    public void ApplyTheme(string themeName)
    {
        if (!AvailableThemes.Contains(themeName, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unknown theme: {themeName}", nameof(themeName));

        var toRemove = _app.Resources.MergedDictionaries
            .Where(d => d.Source != null &&
                        d.Source.OriginalString.Contains("/Themes/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var dic in toRemove)
            _app.Resources.MergedDictionaries.Remove(dic);

        // Always seed shared resources from the base theme so overlays can override selectively.
        AddThemeDictionary(BaseThemeUri);

        if (!string.Equals(themeName, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            var themeUri = BuildThemeUri(themeName);
            AddThemeDictionary(themeUri);
        }

        CurrentTheme = themeName;
    }

    private void AddThemeDictionary(Uri uri) =>
        _app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });

    private static Uri BuildThemeUri(string themeName) =>
        new($"/PackTracker.Presentation;component/{ThemeFolder}{themeName}.xaml", UriKind.Relative);
}
