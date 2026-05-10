using System.Collections.ObjectModel;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly PackTrackerApiClient _api;
    private readonly ObservableCollection<DashboardCard> _activeRequests = new();
    private readonly ObservableCollection<DashboardCard> _myActiveTasks  = new();
    private readonly ObservableCollection<DashboardCard> _myOpenRequests = new();

    public DashboardPage(PackTrackerApiClient api)
    {
        InitializeComponent();
        _api = api;
        ActiveRequestsView.ItemsSource = _activeRequests;
        MyActiveTasksView.ItemsSource  = _myActiveTasks;
        MyOpenRequestsView.ItemsSource = _myOpenRequests;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDashboardAsync().ConfigureAwait(false);
    }

    private async void RefreshView_Refreshing(object? sender, EventArgs e)
    {
        await LoadDashboardAsync().ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() => PageRefreshView.IsRefreshing = false);
    }

    private async Task LoadDashboardAsync()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = "Loading dashboard...");

            var profileTask = _api.GetAsync<CurrentUserSummary>("api/v1/profiles/me");
            var summaryTask = _api.GetAsync<DashboardSummaryDto>("api/v1/dashboard/summary");

            await Task.WhenAll(profileTask, summaryTask).ConfigureAwait(false);

            var profile = await profileTask;
            var summary = await summaryTask;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HeadingLabel.Text = profile is null
                    ? "OPERATIONS DASHBOARD"
                    : $"WELCOME, {profile.DiscordDisplayName ?? profile.Username}".ToUpperInvariant();

                ReplaceCards(_activeRequests, summary?.ActiveRequests);
                ReplaceCards(_myActiveTasks,  summary?.PersonalContext?.MyActiveTasks);
                ReplaceCards(_myOpenRequests, summary?.PersonalContext?.MyPendingRequests);

                var activeCount = summary?.ActiveRequests?.Count ?? 0;
                RequestCountLabel.Text = activeCount > 0 ? $"{activeCount}" : string.Empty;

                StatusLabel.Text = summary is null
                    ? "Could not load dashboard."
                    : $"{activeCount} active request{(activeCount == 1 ? "" : "s")} loaded.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Dashboard failed: {ex.Message}";
                PageRefreshView.IsRefreshing = false;
            });
        }
    }

    private static void ReplaceCards(
        ObservableCollection<DashboardCard> target,
        IReadOnlyList<ActiveRequestDto>? source)
    {
        target.Clear();

        if (source is null || source.Count == 0)
            return;

        foreach (var item in source)
        {
            var requester = string.IsNullOrWhiteSpace(item.RequesterDisplayName)
                ? "Unknown requester"
                : item.RequesterDisplayName;
            var assignee = string.IsNullOrWhiteSpace(item.AssigneeDisplayName)
                ? "Unassigned"
                : item.AssigneeDisplayName;
            target.Add(new DashboardCard(
                item.Title,
                $"{item.RequestType} • {item.Status} • {requester} • {assignee}"));
        }
    }

    private sealed record DashboardCard(string Title, string StatusLine);

    private sealed record CurrentUserSummary(string Username, string? DiscordDisplayName);
}
