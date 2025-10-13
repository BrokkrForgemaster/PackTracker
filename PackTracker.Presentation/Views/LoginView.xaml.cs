using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

/// <summary>
/// Handles the Discord authentication stage before main app access.
/// Automatically ensures the embedded API is running and detects login completion.
/// </summary>
public partial class LoginView : UserControl
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LoginView> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IKillEventService _killEventService;
    private static bool _apiStarted;
    private bool _discordLinked;

    public LoginView(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = _serviceProvider.GetRequiredService<ILogger<LoginView>>();
        _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        _killEventService = _serviceProvider.GetRequiredService<IKillEventService>();

        _ = InitializeAsync();
    }

    /// <summary>
    /// Ensures the API is running and updates status UI.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            DiscordStatus.Text = "Starting local API...";
            await EnsureApiRunningAsync();
            DiscordStatus.Text = "✅ API running locally. Ready for login.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start local API.");
            DiscordStatus.Text = $"❌ Failed to start API: {ex.Message}";
            ProceedBtn.IsEnabled = false;
        }
    }

    /// <summary>
    /// Starts the embedded API if it hasn't been started yet.
    /// </summary>
    private async Task EnsureApiRunningAsync()
    {
        if (_apiStarted)
        {
            _logger.LogInformation("API already running — skipping start.");
            return;
        }

        try
        {
            // Check if the API is already alive (e.g., from main host)
            using var client = new HttpClient();
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var resp = await client.GetAsync("http://localhost:5001/swagger");
                    if (resp.IsSuccessStatusCode)
                    {
                        _apiStarted = true;
                        _logger.LogInformation("Detected running embedded API.");
                        return;
                    }
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }

            // Try to start manually only if not found
            if (!_apiStarted)
            {
                var apiHost = _serviceProvider.GetService<ApiHostedService>();
                if (apiHost != null)
                {
                    await apiHost.StartAsync(default);
                    _apiStarted = true;
                    _logger.LogInformation("Embedded API manually started on http://localhost:5001");
                }
                else
                {
                    throw new InvalidOperationException("API host service not available.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while ensuring embedded API is running.");
            throw;
        }
    }

    /// <summary>
    /// Called when the user clicks "Login with Discord".
    /// </summary>
    private async void DiscordLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DiscordStatus.Text = "Checking API availability...";

            var baseUrl = $"http://localhost:5001/api/v1/auth/login";

            bool apiReady = await WaitForApiAsync(baseUrl);

            if (!apiReady)
            {
                DiscordStatus.Text = $"❌ Unable to reach local API (http://localhost:5001/api/v1/auth/login).";
                return;
            }

            // Open login flow in default browser
            DiscordStatus.Text = "Opening Discord login...";
            var loginUrl = $"{baseUrl}";
            Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true });

            _logger.LogInformation("Discord OAuth login initiated.");

            _ = MonitorLoginCompletionAsync(); // start watching for login
            DiscordStatus.Text = "🔗 Waiting for Discord authentication...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord login failed.");
            DiscordStatus.Text = $"❌ Error: {ex.Message}";
        }

        _discordLinked = true;
        ProceedBtn.IsEnabled = true;
    }

    /// <summary>
    /// Polls settings for a stored access token to confirm login success.
    /// </summary>
    private async Task MonitorLoginCompletionAsync()
    {
        for (int attempt = 0; attempt < 30; attempt++) // ~30s max wait
        {
            var settings = _settingsService.GetSettings();
            if (!string.IsNullOrWhiteSpace(settings.DiscordAccessToken))
            {
                _discordLinked = true;

                await Dispatcher.InvokeAsync(() =>
                {
                    DiscordStatus.Text = "✅ Discord authentication complete.";
                    DiscordCheck.Visibility = Visibility.Visible;
                    ProceedBtn.IsEnabled = true;
                });

                _logger.LogInformation("Discord authentication confirmed.");
                return;
            }

            await Task.Delay(1000);
        }

        await Dispatcher.InvokeAsync(() =>
        {
            DiscordStatus.Text = "⚠️ Timed out waiting for Discord authentication.";
        });
    }

    /// <summary>
    /// Verifies the API is responsive before attempting login.
    /// </summary>
    private async static Task<bool> WaitForApiAsync(string baseUrl)
    {
        using var client = new HttpClient();
        for (int i = 0; i < 6; i++)
        {
            try
            {
                var response = await client.GetAsync(baseUrl);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
                await Task.Delay(750);
            }
        }
        return false;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow window)
            window.ContentFrame.Navigate(new WelcomeView(_serviceProvider));
    }

    private void ProceedBtn_Click(object sender, RoutedEventArgs e)
    {

        if (_discordLinked && Window.GetWindow(this) is MainWindow window)
        {
            window.ContentFrame.Navigate(
                new DashboardView(
                    _serviceProvider.GetRequiredService<IKillEventService>()));
        }
        else
        {
            MessageBox.Show(
                "Please log in with Discord before proceeding.",
                "Authentication Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
