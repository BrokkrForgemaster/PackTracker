using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Services;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.Views;

public partial class LoginView : UserControl
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LoginView> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IApiClientProvider _apiClientProvider;
    private static bool _apiStarted;
    private bool _hasNavigated;
    private string? _clientState;

    public LoginView(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = _serviceProvider.GetRequiredService<ILogger<LoginView>>();
        _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        _apiClientProvider = _serviceProvider.GetRequiredService<IApiClientProvider>();

        _ = InitializeAsync();
    }

    private string ApiBaseUrl => _apiClientProvider.BaseUrl;

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
            DiscordLoginButton.IsEnabled = false;
        }
    }

    private async Task EnsureApiRunningAsync()
    {
        if (_apiStarted)
        {
            _logger.LogInformation("API already running — skipping start.");
            return;
        }

        try
        {
            using var client = new HttpClient();
            var healthEndpoint = $"{ApiBaseUrl}/health";
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var resp = await client.GetAsync(healthEndpoint);
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

            if (!_apiStarted)
            {
                var apiHost = _serviceProvider.GetService<ApiHostedService>();
                if (apiHost != null)
                {
                    await apiHost.StartAsync(default);
                    _apiStarted = true;
                    _logger.LogInformation("Embedded API manually started on {Url}", ApiBaseUrl);
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

    private async void DiscordLogin_Click(object sender, RoutedEventArgs e)
    {
        _clientState = Guid.NewGuid().ToString("N");
        _hasNavigated = false;
        DiscordCheck.Visibility = Visibility.Collapsed;
        var baseUrl = ApiBaseUrl;
        var loginUrl = $"{baseUrl}/api/v1/auth/login?clientState={_clientState}";
        var healthUrl = $"{baseUrl}/health";

        try
        {
            DiscordLoginButton.IsEnabled = false;
            DiscordStatus.Text = "Checking API availability...";

            var apiReady = await WaitForApiAsync(healthUrl);

            if (!apiReady)
            {
                DiscordStatus.Text = $"❌ Unable to reach local API ({baseUrl}).";
                DiscordLoginButton.IsEnabled = true;
                return;
            }

            DiscordStatus.Text = "Opening Discord login...";
            Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true });

            _logger.LogInformation("Discord OAuth login initiated.");
            DiscordStatus.Text = "🔗 Waiting for Discord authentication. Close the browser window when it says you're done.";

            _ = MonitorLoginCompletionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord login failed.");
            DiscordStatus.Text = $"❌ Error: {ex.Message}";
            DiscordLoginButton.IsEnabled = true;
        }
    }

    private async Task MonitorLoginCompletionAsync()
    {
        for (int attempt = 0; attempt < 90; attempt++)
        {
            if (!string.IsNullOrWhiteSpace(_clientState))
            {
                try
                {
                    using var client = _apiClientProvider.CreateClient();
                    var response = await client.GetAsync($"api/v1/auth/poll/{_clientState}");
                    if (response.IsSuccessStatusCode)
                    {
                        var payload = await response.Content.ReadFromJsonAsync<TokenPayload>();
                        if (payload is not null)
                        {
                            _clientState = null;
                            await _settingsService.UpdateSettingsAsync(s =>
                            {
                                s.JwtToken = payload.access_token;
                                s.JwtRefreshToken = payload.refresh_token;
                                s.FirstRunComplete = true;
                            });

                            await Dispatcher.InvokeAsync(() =>
                            {
                                DiscordStatus.Text = "✅ Authentication complete. Redirecting...";
                                DiscordCheck.Visibility = Visibility.Visible;
                            });

                            NavigateToDashboardInternal();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Polling for OAuth completion failed.");
                }
            }

            await Task.Delay(1000);
        }

        await Dispatcher.InvokeAsync(() =>
        {
            DiscordStatus.Text = "⚠️ Timed out waiting for Discord authentication.";
            DiscordLoginButton.IsEnabled = true;
        });
    }

    private void NavigateToDashboardInternal()
    {
        if (_hasNavigated)
            return;

        _hasNavigated = true;

        _ = Dispatcher.InvokeAsync(() =>
        {
            if (Window.GetWindow(this) is not MainWindow window)
                return;

            var dashboardView = _serviceProvider.GetRequiredService<DashboardView>();
            window.ContentFrame.Navigate(dashboardView);
        });
    }

    private async void RetryApi_Click(object sender, RoutedEventArgs e)
    {
        DiscordLoginButton.IsEnabled = false;
        DiscordCheck.Visibility = Visibility.Collapsed;
        DiscordStatus.Text = "Re-checking local API readiness...";

        try
        {
            await EnsureApiRunningAsync();
            DiscordStatus.Text = "✅ API running locally. Ready for login.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retrying API readiness failed.");
            DiscordStatus.Text = $"❌ Failed to start API: {ex.Message}";
        }
        finally
        {
            DiscordLoginButton.IsEnabled = true;
        }
    }

    private static async Task<bool> WaitForApiAsync(string url)
    {
        using var client = new HttpClient();
        for (int i = 0; i < 6; i++)
        {
            try
            {
                var response = await client.GetAsync(url);
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
}

internal record TokenPayload(string access_token, string refresh_token, int expires_in);
