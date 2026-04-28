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
        StatusPill.Visibility = Visibility.Visible;

        try
        {
            var apiReady = await WaitForApiAsync(ApiBaseUrl);
            DiscordStatus.Text = apiReady
                ? "API reachable. Ready for login."
                : $"API unavailable: {ApiBaseUrl}";

            DiscordLoginButton.IsEnabled = apiReady;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate API configuration.");
            DiscordStatus.Text = ex.Message;
            DiscordLoginButton.IsEnabled = false;
        }
    }

    private async void DiscordLogin_Click(object sender, RoutedEventArgs e)
    {
        _clientState = Guid.NewGuid().ToString("N");
        _hasNavigated = false;
        DiscordCheck.Visibility = Visibility.Collapsed;
        var baseUrl = ApiBaseUrl;
        var loginUrl = $"{baseUrl}/api/v1/auth/login?clientState={_clientState}";

        try
        {
            DiscordLoginButton.IsEnabled = false;
            DiscordStatus.Text = "Checking API availability...";

            var apiReady = await WaitForApiAsync(baseUrl);

            if (!apiReady)
            {
                DiscordStatus.Text = $"Unable to reach API ({baseUrl}).";
                DiscordLoginButton.IsEnabled = true;
                return;
            }

            DiscordStatus.Text = "Opening Discord login...";
            Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true });

            _logger.LogInformation("Discord OAuth login initiated.");
            DiscordStatus.Text = "Waiting for Discord authentication. Close the browser window when it says you're done.";

            _ = MonitorLoginCompletionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord login failed.");
            DiscordStatus.Text = $"Error: {ex.Message}";
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
                                DiscordStatus.Text = "Authentication complete. Redirecting...";
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
            DiscordStatus.Text = "Timed out waiting for Discord authentication.";
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

            window.NavigateToDashboard();
        });
    }

    private async void RetryApi_Click(object sender, RoutedEventArgs e)
    {
        DiscordLoginButton.IsEnabled = false;
        DiscordCheck.Visibility = Visibility.Collapsed;
        DiscordStatus.Text = "Re-checking API availability...";

        try
        {
            var apiReady = await WaitForApiAsync(ApiBaseUrl);
            DiscordStatus.Text = apiReady
                ? "API reachable. Ready for login."
                : $"API unavailable: {ApiBaseUrl}";
            DiscordLoginButton.IsEnabled = apiReady;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retrying API availability failed.");
            DiscordStatus.Text = ex.Message;
        }
    }

    private static async Task<bool> WaitForApiAsync(string baseUrl)
    {
        using var client = new HttpClient();
        var readinessUrl = $"{baseUrl.TrimEnd('/')}/health/ready";
        var livenessUrl = $"{baseUrl.TrimEnd('/')}/health";
        for (int i = 0; i < 6; i++)
        {
            try
            {
                var readinessResponse = await client.GetAsync(readinessUrl);
                if (readinessResponse.IsSuccessStatusCode)
                    return true;

                if (readinessResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var livenessResponse = await client.GetAsync(livenessUrl);
                    if (livenessResponse.IsSuccessStatusCode)
                        return true;
                }
            }
            catch
            {
            }

            await Task.Delay(750);
        }

        return false;
    }
}

internal record TokenPayload(string access_token, string refresh_token, int expires_in);
