using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views
{
    /// <summary name="MainWindow">
    /// Main application window with navigation and sidebar timer logic.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly DispatcherTimer _timer;

        #endregion

        #region Constructor

        public MainWindow(IServiceProvider serviceProvider, ISettingsService settingsService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            InitializeComponent();

            WindowState = WindowState.Maximized;

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
            var settings = _settingsService.GetSettings();

            // First-run setup not complete → go to Settings (Welcome) to capture keys/paths, etc.
            if (!settings.FirstRunComplete)
            {
                NavigateToSettings();
                return;
            }

            // We authenticate users with a JWT access token saved in settings (NOT JwtKey).
            var jwtToken = settings.JwtToken;

            // If token missing or invalid/expired → go to Login
            if (string.IsNullOrWhiteSpace(jwtToken) || !IsJwtValid(jwtToken))
            {
                NavigateToLogin();
                return;
            }

            // Otherwise, proceed to dashboard
            NavigateToDashboard();
        }

        /// <summary>
        /// Defensive JWT validation (structure + expiration). Never throws.
        /// </summary>
        private bool IsJwtValid(string token)
        {
            // quick structure check: JWT must have three segments
            if (string.IsNullOrWhiteSpace(token) || token.Count(c => c == '.') != 2)
                return false;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                // If token has no 'exp', treat as invalid
                var expiresUtc = jwt.ValidTo;
                if (expiresUtc == default)
                    return false;

                return expiresUtc > DateTime.UtcNow;
            }
            catch
            {
                return false;
            }
        }

        private void NavigateToLogin()
        {
            var loginView = _serviceProvider.GetRequiredService<LoginView>();
            ContentFrame.Navigate(loginView);
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
                    case "TradingHub":
                        NavigateToUex();
                        break;
                    case "RequestHub":
                        NavigateToRequestsHub();
                        break;
                    case "Blueprints":
                        NavigateToBlueprintExplorer();
                        break;
                    case "CraftingQueue":
                        NavigateToCraftingQueue();
                        break;
                    case "ProcurementQueue":
                        NavigateToProcurementQueue();
                        break;
                    case "Settings":
                        NavigateToSettings();
                        break;
                }
            }
        }

        private void NavigateToDashboard()
        {
            var dashboardView = _serviceProvider.GetRequiredService<DashboardView>();
            ContentFrame.Navigate(dashboardView);
        }

        private void NavigateToUex()
        {
            var uexView = _serviceProvider.GetRequiredService<UexView>();
            ContentFrame.Navigate(uexView);
        }


        private void NavigateToRequestsHub()
        {
            try
            {
                var requestView = _serviceProvider.GetRequiredService<RequestsView>();
                ContentFrame.Navigate(requestView);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to navigate to Requests Hub");
                
                MessageBox.Show($"Failed to open Requests Hub:\n{ex}", "Navigation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToBlueprintExplorer()
        {
            try
            {
                var blueprintView = _serviceProvider.GetRequiredService<BlueprintExplorerView>();
                ContentFrame.Navigate(blueprintView);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to navigate to Blueprint Explorer");

                MessageBox.Show($"Failed to open Blueprint Explorer:\n{ex}", "Navigation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToCraftingQueue()
        {
            try
            {
                var craftingView = _serviceProvider.GetRequiredService<CraftingRequestsView>();
                ContentFrame.Navigate(craftingView);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to navigate to Crafting Queue");

                MessageBox.Show($"Failed to open Crafting Queue:\n{ex}", "Navigation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToProcurementQueue()
        {
            try
            {
                var procurementView = _serviceProvider.GetRequiredService<ProcurementRequestsView>();
                ContentFrame.Navigate(procurementView);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to navigate to Procurement Queue");

                MessageBox.Show($"Failed to open Procurement Queue:\n{ex}", "Navigation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToSettings()
        {
            var themeManager = _serviceProvider.GetService<IThemeManager>();
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();

            if (_settingsService != null && themeManager != null && loggerFactory != null)
            {
                var logger = loggerFactory.CreateLogger<SettingsView>();
                var settingsView = new SettingsView(_serviceProvider, _settingsService, themeManager, logger);
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
}
