using System.Collections.ObjectModel;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

/// <summary>
/// Display card for a single org member in the members list.
/// </summary>
public sealed class MemberCard
{
    public string  Username    { get; init; } = string.Empty;
    public string  DisplayName { get; init; } = string.Empty;
    public string  RoleLine    { get; init; } = string.Empty;
    public bool    IsOnline    { get; init; }
    public Color   OnlineColor => IsOnline
        ? Color.FromArgb("#00A86B")  // GreenActive
        : Color.FromArgb("#6E5A46"); // TextMuted
}

public partial class MembersPage : ContentPage
{
    private readonly PackTrackerApiClient _api;
    private readonly MobileChatService    _chat;

    private readonly ObservableCollection<MemberCard> _members = new();

    public MembersPage(PackTrackerApiClient api, MobileChatService chatService)
    {
        InitializeComponent();
        _api  = api;
        _chat = chatService;
        MembersView.ItemsSource = _members;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMembersAsync().ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────
    // Data loading
    // ──────────────────────────────────────────────────────────────────

    private async Task LoadMembersAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = "Loading members...");

        try
        {
            // Fetch all profiles and the online set concurrently
            var allTask    = _api.GetAsync<List<MemberProfileDto>>("api/v1/profiles");
            var onlineTask = _api.GetAsync<List<OnlineProfileDto>>("api/v1/profiles/online");

            await Task.WhenAll(allTask, onlineTask).ConfigureAwait(false);

            var allProfiles    = await allTask    ?? new List<MemberProfileDto>();
            var onlineProfiles = await onlineTask ?? new List<OnlineProfileDto>();

            // Build a set of online usernames for O(1) lookup
            var onlineSet = new HashSet<string>(
                onlineProfiles.Select(p => p.Username ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            // Sort: online first, then alphabetically by display name
            var cards = allProfiles
                .Select(p => new MemberCard
                {
                    Username    = p.Username ?? string.Empty,
                    DisplayName = p.DiscordDisplayName ?? p.Username ?? string.Empty,
                    RoleLine    = BuildRoleLine(p.DiscordRank, p.DiscordDivision),
                    IsOnline    = onlineSet.Contains(p.Username ?? string.Empty),
                })
                .OrderByDescending(c => c.IsOnline)
                .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _members.Clear();
                foreach (var card in cards)
                    _members.Add(card);

                var onlineCount = cards.Count(c => c.IsOnline);
                StatusLabel.Text = $"{onlineCount} online · {cards.Count} total";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                StatusLabel.Text = $"Failed to load members: {ex.Message}");
        }
    }

    private static string BuildRoleLine(string? rank, string? division)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(rank))     parts.Add($"Rank: {rank}");
        if (!string.IsNullOrWhiteSpace(division)) parts.Add($"Division: {division}");
        return parts.Count > 0 ? string.Join(" • ", parts) : "Member";
    }

    // ──────────────────────────────────────────────────────────────────
    // UI event handlers
    // ──────────────────────────────────────────────────────────────────

    private async void DmButton_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not MemberCard member)
            return;

        // Navigate to ChatPage with DM query parameters
        var encodedName = Uri.EscapeDataString(member.DisplayName);
        await Shell.Current.GoToAsync(
            $"//Chat?dm={member.Username}&displayName={encodedName}").ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────
    // Local DTOs (avoids coupling to Application layer model shapes)
    // ──────────────────────────────────────────────────────────────────

    private sealed record MemberProfileDto(
        string?  Username,
        string?  DiscordDisplayName,
        string?  DiscordRank,
        string?  DiscordDivision);

    private sealed record OnlineProfileDto(
        string? Username);
}
