namespace PackTracker.Application.Interfaces
{
    /// <summary>
    /// Interface for managing themes in the application.
    /// </summary>
    public interface IThemeManager
    {
        /// <summary>
        /// Array of available theme names (e.g., "Dark", "Locops", etc.).
        /// </summary>
        string[] AvailableThemes { get; }

        /// <summary>
        /// The currently applied theme name.
        /// </summary>
        string CurrentTheme { get; }

        /// <summary>
        /// Applies the specified theme by name.
        /// </summary>
        /// <param name="themeName">The name of the theme to apply.</param>
        void ApplyTheme(string themeName);
    }
}