using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly MobileSessionService _session;
    private readonly MobileAuthService _auth;
    private string? _clientState;

    public LoginPage(MobileSessionService session, MobileAuthService auth)
    {
        InitializeComponent();
        _session = session;
        _auth = auth;
        ApiBaseUrlEntry.Text = _session.GetApiBaseUrl();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (await _auth.IsSignedInAsync().ConfigureAwait(false))
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync("//Dashboard");
            });
            return;
        }

        await CheckApiAsync().ConfigureAwait(false);
    }

    private async void LoginButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            SetBusy(true, "Opening Discord login...");
            _clientState = await _auth.StartLoginAsync().ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ResumeLoginButton.IsVisible = true;
                StatusLabel.Text = "Browser opened. Finish Discord login, then tap 'I FINISHED LOGIN'.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SetBusy(false, $"Login failed: {ex.Message}");
            });
        }
    }

    private async void ResumeLoginButton_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_clientState))
        {
            StatusLabel.Text = "Start login first.";
            return;
        }

        try
        {
            SetBusy(true, "Waiting for Discord authentication...");
            var complete = await _auth.PollLoginAsync(_clientState).ConfigureAwait(false);
            if (!complete)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SetBusy(false, "Login timed out. Start the login flow again.");
                });
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                SetBusy(false, "Authentication complete.");
                await Shell.Current.GoToAsync("//Dashboard");
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SetBusy(false, $"Login polling failed: {ex.Message}");
            });
        }
    }

    private async void SaveApiUrlButton_Clicked(object sender, EventArgs e)
    {
        _session.SetApiBaseUrl(ApiBaseUrlEntry.Text ?? string.Empty);
        await CheckApiAsync().ConfigureAwait(false);
    }

    private async Task CheckApiAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() => SetBusy(true, "Checking API availability..."));
        var ready = await _auth.WaitForApiAsync().ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            SetBusy(false, ready
                ? $"API reachable: {_session.GetApiBaseUrl()}"
                : $"API unavailable: {_session.GetApiBaseUrl()}");
            LoginButton.IsEnabled = ready;
        });
    }

    private void SetBusy(bool isBusy, string status)
    {
        LoginButton.IsEnabled = !isBusy;
        StatusLabel.Text = status;
    }
}
