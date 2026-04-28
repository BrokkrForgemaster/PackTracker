using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using Microsoft.Extensions.Logging;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.Views;

public partial class SettingsView : UserControl
{
    #region Fields & Constructor

    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly IThemeManager _themeManager;
    private readonly BackendDiagnosticsService _backendDiagnosticsService;
    private readonly ILogger<SettingsView> _logger;
    private AppSettings _current;

    public SettingsView(
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        IThemeManager themeManager,
        BackendDiagnosticsService backendDiagnosticsService,
        ILogger<SettingsView> logger)
    {
        InitializeComponent();
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _backendDiagnosticsService = backendDiagnosticsService ?? throw new ArgumentNullException(nameof(backendDiagnosticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _current = _settingsService.GetSettings();
        _serviceProvider = serviceProvider;

        CmbThemes.ItemsSource = _themeManager.AvailableThemes;
        CmbThemes.SelectedItem = _current.Theme ?? _themeManager.CurrentTheme;
        TxtUexcorpApiKey.Text = _current.UexCorpApiKey ?? string.Empty;
        TxtGameLogFilePath.Text = _current.GameLogFilePath ?? string.Empty;

        Loaded += async (_, _) => await RefreshDiagnosticsAsync();
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

    private async void RefreshDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDiagnosticsAsync();
    }

    private async Task RefreshDiagnosticsAsync()
    {
        try
        {
            BtnRefreshDiagnostics.IsEnabled = false;
            TxtBackendOverallStatus.Text = "Checking backend status...";
            TxtBackendSummary.Text = "Querying API liveness, readiness, and diagnostic status.";
            TxtBackendDetails.Text = string.Empty;

            var snapshot = await _backendDiagnosticsService.GetSnapshotAsync();
            ApplyDiagnostics(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh backend diagnostics in settings view.");
            ApplyDiagnostics(new BackendDiagnosticsSnapshot
            {
                IsApiReachable = false,
                IsReady = false,
                ReadinessSummary = ex.Message,
                PingStatus = "unavailable",
                DiagnosticsErrorMessage = ex.Message
            });
        }
        finally
        {
            BtnRefreshDiagnostics.IsEnabled = true;
        }
    }

    private void ApplyDiagnostics(BackendDiagnosticsSnapshot snapshot)
    {
        if (!snapshot.IsApiReachable)
        {
            BackendStatusBorder.Background = new SolidColorBrush(Color.FromRgb(58, 24, 24));
            BackendStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(150, 72, 72));
            TxtBackendOverallStatus.Text = "Backend unreachable";
            TxtBackendSummary.Text = $"The client could not contact the API at {_settingsService.GetSettings().ApiBaseUrl}.";
            TxtBackendDetails.Text = snapshot.DiagnosticsErrorMessage ?? snapshot.ReadinessSummary;
            return;
        }

        var isHealthy = snapshot.IsReady
            && snapshot.CanConnect == true
            && snapshot.PendingMigrations.Count == 0
            && snapshot.StartupInitialized != false;

        if (isHealthy)
        {
            BackendStatusBorder.Background = new SolidColorBrush(Color.FromRgb(18, 46, 30));
            BackendStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 136, 98));
            TxtBackendOverallStatus.Text = "Backend healthy";
        }
        else
        {
            BackendStatusBorder.Background = new SolidColorBrush(Color.FromRgb(58, 46, 22));
            BackendStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(163, 123, 46));
            TxtBackendOverallStatus.Text = "Backend needs attention";
        }

        TxtBackendSummary.Text =
            $"Readiness: {(snapshot.IsReady ? "ready" : $"not ready ({(int?)snapshot.ReadinessStatusCode})")}. " +
            $"Startup initialized: {FormatBoolean(snapshot.StartupInitialized)}. " +
            $"Database reachable: {FormatBoolean(snapshot.CanConnect)}.";

        var details = new List<string>
        {
            $"Provider: {snapshot.Provider ?? "unknown"}",
            $"Ping status: {snapshot.PingStatus}",
            $"Applied migrations: {(snapshot.AppliedMigrationsCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown")}"
        };

        if (snapshot.PendingMigrations.Count > 0)
            details.Add($"Pending migrations: {string.Join(", ", snapshot.PendingMigrations)}");

        if (!string.IsNullOrWhiteSpace(snapshot.StartupFailureMessage))
            details.Add($"Startup failure: {snapshot.StartupFailureMessage}");

        if (!string.IsNullOrWhiteSpace(snapshot.DiagnosticsErrorMessage))
            details.Add($"Diagnostics error: {snapshot.DiagnosticsErrorMessage}");

        if (!string.IsNullOrWhiteSpace(snapshot.ReadinessSummary))
            details.Add($"Readiness summary: {snapshot.ReadinessSummary}");

        TxtBackendDetails.Text = string.Join(Environment.NewLine, details);
    }

    private static string FormatBoolean(bool? value) =>
        value.HasValue ? (value.Value ? "yes" : "no") : "unknown";

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
        _ = RefreshDiagnosticsAsync();
    }

    #endregion
}
