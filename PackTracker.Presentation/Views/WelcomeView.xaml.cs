using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Services;

namespace PackTracker.Presentation.Views;

/// <summary name="WelcomeView">
/// WelcomeView handles first-run setup for PackTracker,
/// including required integrations, opt-out logic, and proceeding to main app.
/// </summary>
public partial class WelcomeView : UserControl
{
    #region Fields & Constructor

    private readonly IServiceProvider _serviceProvider;
    private bool _discordLinked = false; // True if Discord is connected

    public WelcomeView(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        CheckAllIntegrations();
        UpdateProceedButtonState();
    }

    #endregion

    #region Integration Status & State Logic

    private void CheckAllIntegrations()
    {
        RegolithCheck.Visibility = !string.IsNullOrWhiteSpace(RegolithApiKeyBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        RegolithStatus.Text = !string.IsNullOrWhiteSpace(RegolithApiKeyBox.Text)
            ? "API key detected! You're connected."
            : "No API key found. Please paste and save your key or opt out.";

        UexcorpCheck.Visibility = !string.IsNullOrWhiteSpace(UexcorpApiKeyBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        UexcorpStatus.Text = !string.IsNullOrWhiteSpace(UexcorpApiKeyBox.Text)
            ? "API key detected! You're connected."
            : "No API key found. Please paste and save your key or opt out.";

        LogCheck.Visibility = !string.IsNullOrWhiteSpace(LogLocationBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        LogLocationStatus.Text = !string.IsNullOrWhiteSpace(LogLocationBox.Text)
            ? "Star Citizen log folder detected."
            : "No log folder detected. Please set a valid path or opt out.";

        UpdateProceedButtonState();
    }

    #endregion

    #region Opt-Out CheckBox Events

    private void OptOutCheckBox_Checked(object sender, RoutedEventArgs e) => UpdateProceedButtonState();
    private void OptOutCheckBox_Unchecked(object sender, RoutedEventArgs e) => UpdateProceedButtonState();

    #endregion

    #region Save, Connect, and Hyperlink Handlers

    private void SaveRegolithApiKey_Click(object sender, RoutedEventArgs e)
    {
        // Example: SecretStorage.Protect(RegolithApiKeyBox.Text);
        CheckAllIntegrations();
    }

    /// <summary>
    /// Handles save for UEXCorp API key.
    /// TODO: Securely store the key with SecretStorage here.
    /// </summary>
    private void SaveUexCorpApiKey_Click(object sender, RoutedEventArgs e)
    {
        SecretStorage.Protect(UexcorpApiKeyBox.Text);
        SecretStorage.Protect(RegolithApiKeyBox.Text);
        SecretStorage.Protect(LogLocationBox.Text);
        CheckAllIntegrations();
    }

    private void ConnectDiscord_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Integrate Discord OAuth2 authentication and securely store result.
        _discordLinked = true;
        CheckAllIntegrations();
    }

    private void SaveLogLocation_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Securely store the path as needed.
        CheckAllIntegrations();
    }

    private void RegolithLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void UexcorpLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    #endregion

    #region Proceed Button Logic

    private void UpdateProceedButtonState()
    {
        bool regolithComplete = !string.IsNullOrWhiteSpace(RegolithApiKeyBox.Text) ||
                                (RegolithOptOutCheckBox.IsChecked == true);
        bool uexcorpComplete = !string.IsNullOrWhiteSpace(UexcorpApiKeyBox.Text) ||
                               (UexcorpOptOutCheckBox.IsChecked == true);
        bool logComplete = !string.IsNullOrWhiteSpace(LogLocationBox.Text) || (LogOptOutCheckBox.IsChecked == true);

        ProceedBtn.IsEnabled = regolithComplete && uexcorpComplete && logComplete;
    }

    private async void ProceedBtn_Click(object sender, RoutedEventArgs e)
    {
        ProceedBtn.IsEnabled = false;

        try
        {
            var settingsService = _serviceProvider.GetService<ISettingsService>();
            if (settingsService != null)
            {
                var settings = settingsService.GetSettings();
                settings.FirstRunComplete = true;
                await settingsService.UpdateSettingsAsync( s =>
                {
                    s.RegolithApiKey = RegolithApiKeyBox.Text.Trim();
                    s.UexCorpApiKey = UexcorpApiKeyBox.Text.Trim();
                    s.GameLogFilePath = LogLocationBox.Text.Trim();
                    s.FirstRunComplete = true;
                });
            }

            if (Window.GetWindow(this) is MainWindow window)
                window.ContentFrame.Navigate(new DashboardView());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred while saving settings: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ProceedBtn.IsEnabled = true;
        }
    }

    #endregion
}