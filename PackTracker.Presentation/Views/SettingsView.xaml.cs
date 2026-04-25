using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace PackTracker.Presentation.Views;

public partial class SettingsView : UserControl
{
    #region Fields & Constructor

    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly IThemeManager _themeManager;
    private readonly ILogger<SettingsView> _logger;
    private AppSettings _current;

    public SettingsView(
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        IThemeManager themeManager,
        ILogger<SettingsView> logger)
    {
        InitializeComponent();
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _current = _settingsService.GetSettings();
        _serviceProvider = serviceProvider;

        CmbThemes.ItemsSource = _themeManager.AvailableThemes;
        CmbThemes.SelectedItem = _current.Theme ?? _themeManager.CurrentTheme;
        TxtUexcorpApiKey.Text = _current.UexCorpApiKey ?? string.Empty;
        TxtGameLogFilePath.Text = _current.GameLogFilePath ?? string.Empty;
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
        var selectedTheme = (CmbThemes.SelectedItem as string) ?? _themeManager.CurrentTheme;
        var uex = TxtUexcorpApiKey.Text.Trim();
        var logPath = TxtGameLogFilePath.Text.Trim();

        try
        {
            await _settingsService.UpdateSettingsAsync(settings =>
            {
                settings.Theme = selectedTheme;
                settings.UexCorpApiKey = uex;
                settings.GameLogFilePath = logPath;
                _current = settings;
            });

            if (!string.IsNullOrWhiteSpace(selectedTheme))
                _themeManager.ApplyTheme(selectedTheme);

            MessageBox.Show("Settings saved successfully.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);

            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ContentFrame.Navigate(_serviceProvider.GetRequiredService<DashboardView>());
                _ = mainWindow.RefreshSidebarProfileAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save settings:\n\n{ex.GetType()}\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Settings cancelled by user.");
        _current = _settingsService.GetSettings();
        CmbThemes.SelectedItem = _current.Theme ?? _themeManager.CurrentTheme;
        TxtUexcorpApiKey.Text = _current.UexCorpApiKey ?? string.Empty;
        TxtGameLogFilePath.Text = _current.GameLogFilePath ?? string.Empty;
    }

    #endregion
}
