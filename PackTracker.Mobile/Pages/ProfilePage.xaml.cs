using PackTracker.Application.DTOs.Profiles;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly PackTrackerApiClient _api;

    public ProfilePage(PackTrackerApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var profile = await _api.GetAsync<CurrentProfileDto>("api/v1/profiles/me").ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (profile is null)
                {
                    StatusLabel.Text = "Could not load profile.";
                    return;
                }

                NameLabel.Text = profile.DiscordDisplayName ?? profile.Username;
                RankLabel.Text = $"Rank: {profile.DiscordRank ?? "Unknown"}";
                DivisionLabel.Text = $"Division: {profile.DiscordDivision ?? "Unknown"}";
                BioLabel.Text = string.IsNullOrWhiteSpace(profile.ShowcaseBio)
                    ? "No profile bio configured."
                    : profile.ShowcaseBio;
                StatusLabel.Text = profile.IsOnline ? "Online" : "Offline";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Profile failed: {ex.Message}";
            });
        }
    }
}
