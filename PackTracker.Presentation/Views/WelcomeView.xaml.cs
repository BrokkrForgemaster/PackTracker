using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.Views;

public partial class WelcomeView : UserControl
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settings;

    public WelcomeView(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _settings = serviceProvider.GetRequiredService<ISettingsService>();

        LoadExistingSettings();
        CheckAllIntegrations();
        UpdateProceedButtonState();
    }

    private void LoadExistingSettings()
    {
        var cfg = _settings.GetSettings();
        UexcorpApiKeyBox.Text = cfg.UexCorpApiKey ?? "";
        LogLocationBox.Text = cfg.GameLogFilePath ?? "";
    }

    private void CheckAllIntegrations()
    {
        UexcorpCheck.Visibility = string.IsNullOrWhiteSpace(UexcorpApiKeyBox.Text)
            ? Visibility.Collapsed : Visibility.Visible;
        UexcorpStatus.Text = string.IsNullOrWhiteSpace(UexcorpApiKeyBox.Text)
            ? "No API key found. Please paste and save your key or opt out."
            : "✅ UEXCorp API key saved.";

        LogCheck.Visibility = string.IsNullOrWhiteSpace(LogLocationBox.Text)
            ? Visibility.Collapsed : Visibility.Visible;
        LogLocationStatus.Text = string.IsNullOrWhiteSpace(LogLocationBox.Text)
            ? "No Star Citizen log path detected. Please browse or opt out."
            : "✅ Log folder saved.";
    }

    #region Save buttons

    private async void SaveUexCorpApiKey_Click(object sender, RoutedEventArgs e)
    {
        await _settings.UpdateSettingsAsync(cfg => cfg.UexCorpApiKey = UexcorpApiKeyBox.Text.Trim());
        CheckAllIntegrations();
        UpdateProceedButtonState();
    }

    private async void SaveLogLocation_Click(object sender, RoutedEventArgs e)
    {
        await _settings.UpdateSettingsAsync(cfg => cfg.GameLogFilePath = LogLocationBox.Text.Trim());
        CheckAllIntegrations();
        UpdateProceedButtonState();
    }

    #endregion

    private void OptOutCheckBox_Checked(object sender, RoutedEventArgs e) => UpdateProceedButtonState();
    private void OptOutCheckBox_Unchecked(object sender, RoutedEventArgs e) => UpdateProceedButtonState();

    private void UexcorpLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void UpdateProceedButtonState()
    {
        bool uexcorpComplete = !string.IsNullOrWhiteSpace(UexcorpApiKeyBox.Text)
                               || UexcorpOptOutCheckBox.IsChecked == true;
        bool logComplete = !string.IsNullOrWhiteSpace(LogLocationBox.Text)
                           || LogOptOutCheckBox.IsChecked == true;

        ProceedBtn.IsEnabled = uexcorpComplete && logComplete;
        ProceedBtn.Opacity = ProceedBtn.IsEnabled ? 1.0 : 0.5;
    }

    private async void ProceedBtn_Click(object sender, RoutedEventArgs e)
    {
        await _settings.UpdateSettingsAsync(cfg =>
        {
            cfg.UexCorpApiKey = UexcorpApiKeyBox.Text.Trim();
            cfg.GameLogFilePath = LogLocationBox.Text.Trim();
            cfg.FirstRunComplete = true;
        });

        if (Window.GetWindow(this) is MainWindow window)
        {
            window.ContentFrame.Navigate(new LoginView(_serviceProvider));
        }
    }
}
