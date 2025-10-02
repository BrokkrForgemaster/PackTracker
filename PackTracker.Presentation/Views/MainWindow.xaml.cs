using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace PackTracker.Presentation.Views;

/// <summary name="MainWindow">
/// Main application window with navigation and sidebar timer logic.
/// </summary>
public partial class MainWindow
{
    #region Fields

    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _timer;

    #endregion

    #region Constructor

    public MainWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        InitializeComponent();

        WindowState = WindowState.Maximized;
        WindowStyle = WindowStyle.None;

        // Sidebar real-time clock
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            TxtLocalTime.Text = DateTime.Now.ToString("MMMM d, yyyy") + "\n" +
                                DateTime.Now.ToString("       HH:mm:ss");
        };
        _timer.Start();

        NavigateToFirstView();
    }

    #endregion

    #region Navigation

    private void NavigateToFirstView()
    {
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var settings = settingsService.GetSettings();

        bool incomplete =
            string.IsNullOrWhiteSpace(settings.RegolithApiKey) ||
            string.IsNullOrWhiteSpace(settings.UexCorpApiKey) ||
            string.IsNullOrWhiteSpace(settings.GameLogFilePath);

        if (incomplete)
            ContentFrame.Navigate(new WelcomeView(_serviceProvider));
        else
            NavigateToDashboard();
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
        {
            switch (tag)
            {
                case "Dashboard":
                    NavigateToDashboard();
                    break;
                case "KillTracker":
                    NavigateToKillTracker();
                    break;
                case "Settings":
                    NavigateToSettings();
                    break;
                // Add more cases as you add features!
            }
        }
    }

    private void NavigateToDashboard()
    {
        var dashboardView = _serviceProvider.GetRequiredService<DashboardView>();
        ContentFrame.Navigate(dashboardView);
    }
    
    private void NavigateToKillTracker()
    {
        try
        {
            var killTrackerView = _serviceProvider.GetRequiredService<KillTracker>();
            ContentFrame.Navigate(killTrackerView);
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
            logger?.LogError(ex, "Failed to navigate to KillTracker");
            MessageBox.Show($"Failed to load KillTracker: {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NavigateToSettings()
    {
        var settingsService = _serviceProvider.GetService<ISettingsService>();
        var themeManager = _serviceProvider.GetService<IThemeManager>();
        var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();

        if (settingsService != null && themeManager != null && loggerFactory != null)
        {
            var logger = loggerFactory.CreateLogger<SettingsView>();
            var settingsView = new SettingsView(settingsService, themeManager, logger);
            ContentFrame.Navigate(settingsView);
        }
        else
        {
            MessageBox.Show("Unable to resolve all required services for settings.", "Dependency Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Sidebar Controls

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateButton.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateStatusText.Visibility = Visibility.Visible;
            UpdateStatusText.Text = "Updating...";

            for (int i = 0; i <= 100; i += 10)
            {
                UpdateProgressBar.Value = i;
                await Task.Delay(200);
            }

            UpdateStatusText.Text = "Update completed!";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatusText.Text = "Update failed.";
        }
        finally
        {
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            UpdateButton.IsEnabled = true;
        }
    }

    #endregion
}