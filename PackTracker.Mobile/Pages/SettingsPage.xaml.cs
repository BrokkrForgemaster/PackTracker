using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly MobileSessionService _session;
    private readonly MobileAuthService _auth;

    public SettingsPage(MobileSessionService session, MobileAuthService auth)
    {
        InitializeComponent();
        _session = session;
        _auth = auth;
        ApiBaseUrlEntry.Text = _session.GetApiBaseUrl();
    }

    private void SaveButton_Clicked(object sender, EventArgs e)
    {
        _session.SetApiBaseUrl(ApiBaseUrlEntry.Text ?? string.Empty);
        StatusLabel.Text = $"Saved API URL: {_session.GetApiBaseUrl()}";
    }

    private async void TestConnectionButton_Clicked(object sender, EventArgs e)
    {
        _session.SetApiBaseUrl(ApiBaseUrlEntry.Text ?? string.Empty);
        StatusLabel.Text = "Checking API...";
        var ready = await _auth.WaitForApiAsync().ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusLabel.Text = ready
                ? $"API reachable: {_session.GetApiBaseUrl()}"
                : $"API unavailable: {_session.GetApiBaseUrl()}";
        });
    }

    private async void LogoutButton_Clicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "Logging out...";
        await _auth.LogoutAsync().ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            StatusLabel.Text = "Logged out.";
            await Shell.Current.GoToAsync("//Login");
        });
    }
}
