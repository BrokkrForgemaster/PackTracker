using System.Windows;
using System.Windows.Controls;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace PackTracker.Presentation.Views;

/// <summary name="SettingsView">
/// The SettingsView allows users to view and update application settings
/// such as theme and API credentials, with secure storage handled by ISettingsService.
/// </summary>
public partial class SettingsView : UserControl
{
    #region Fields & Constructor

    private readonly IServiceProvider _serviceProvider;
    public readonly IKillEventService _killEventService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeManager _themeManager;
    private readonly ILogger<SettingsView> _logger;
    private AppSettings _current;

    public SettingsView(
        IServiceProvider serviceProvider,
        IKillEventService killEventService,
        ISettingsService settingsService,
        IThemeManager themeManager,
        ILogger<SettingsView> logger)
    {
        InitializeComponent();
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _current = _settingsService.GetSettings();

        // Bind only the fields your app uses
        CmbThemes.ItemsSource = _themeManager.AvailableThemes;
        CmbThemes.SelectedItem = _current.Theme ?? _themeManager.CurrentTheme;
        TxtRegolithApiKey.Text = _current.RegolithApiKey ?? string.Empty;
        TxtUexcorpApiKey.Text = _current.UexCorpApiKey ?? string.Empty;
        TxtGameLogFilePath.Text = _current.GameLogFilePath ?? string.Empty;
    }

    public SettingsView(ISettingsService settingsService, IThemeManager killEventService, ILogger<SettingsView> logger)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Theme Selection

    private void cmbThemes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbThemes.SelectedItem is string theme)
            _themeManager.ApplyTheme(theme);

    }

    #endregion

    #region Save & Cancel Buttons

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Settings saved by user.");

        _current.Theme = CmbThemes.SelectedItem as string;
        _current.RegolithApiKey = TxtRegolithApiKey.Text.Trim();
        _current.UexCorpApiKey = TxtUexcorpApiKey.Text.Trim();
        _current.GameLogFilePath = TxtGameLogFilePath.Text.Trim();

        try
        {
            await _settingsService.UpdateSettingsAsync(settings =>
            {
                settings.Theme = _current.Theme;
                settings.RegolithApiKey = _current.RegolithApiKey;
                settings.UexCorpApiKey = _current.UexCorpApiKey;
                settings.GameLogFilePath = _current.GameLogFilePath;
            });

            // Persist and immediately apply the new theme
            if (!string.IsNullOrWhiteSpace(_current.Theme))
                _themeManager.ApplyTheme(_current.Theme);

            MessageBox.Show("Settings saved successfully.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // ✅ Navigate to dashboard (or last view)
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {

                mainWindow.ContentFrame.Navigate(
                    new DashboardView());
                // OR if you have a specific view to navigate to:

                // OR for last view logic:
                // mainWindow.NavigateToLastView();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            MessageBox.Show("Failed to save settings: " + ex.Message,
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }



    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Settings cancelled by user.");

        _current = _settingsService.GetSettings();
        CmbThemes.SelectedItem = _current.Theme ?? _themeManager.CurrentTheme;
        TxtRegolithApiKey.Text = _current.RegolithApiKey ?? string.Empty;
        TxtUexcorpApiKey.Text = _current.UexCorpApiKey ?? string.Empty;
        TxtGameLogFilePath.Text = _current.GameLogFilePath ?? string.Empty;
    }

    #endregion
}
