using System;
using System.Windows.Media;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public sealed class DiscordEventViewModel : ViewModelBase
{
    // Static frozen brushes — thread-safe for WPF binding
    private static readonly Brush LiveBrush    = Freeze(new SolidColorBrush(Color.FromRgb(0x43, 0xB5, 0x81)));
    private static readonly Brush CriticalBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xCC, 0x44, 0x44)));
    private static readonly Brush UrgentBrush  = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0x7A, 0x3A)));
    private static readonly Brush WarnBrush    = Freeze(new SolidColorBrush(Color.FromRgb(0xC8, 0xA9, 0x6E)));
    private static readonly Brush NormalBrush  = Freeze(new SolidColorBrush(Color.FromRgb(0x4F, 0x6A, 0x84)));
    private static readonly Brush MutedBrush   = Freeze(new SolidColorBrush(Color.FromRgb(0x35, 0x45, 0x55)));

    private string _countdownText = string.Empty;
    private Brush _countdownBrush = MutedBrush;
    private Brush _accentBrush = MutedBrush;

    public DiscordEventViewModel(DiscordEventItem item)
    {
        Name = item.Name;
        Description = item.Description;
        StartsAt = item.StartsAt;
        EndsAt = item.EndsAt;
        IsLive = item.Status == 2;
        Location = item.Location;
        HasLocation = !string.IsNullOrWhiteSpace(item.Location);
        InterestedText = item.InterestedCount is > 0 ? $"{item.InterestedCount}" : string.Empty;
        HasInterested = item.InterestedCount is > 0;
    }

    public string Name { get; }
    public string? Description { get; }
    public DateTime StartsAt { get; }
    public DateTime? EndsAt { get; }
    public bool IsLive { get; }
    public string? Location { get; }
    public bool HasLocation { get; }
    public string InterestedText { get; }
    public bool HasInterested { get; }

    public string CountdownText
    {
        get => _countdownText;
        private set => SetProperty(ref _countdownText, value);
    }

    public Brush CountdownBrush
    {
        get => _countdownBrush;
        private set => SetProperty(ref _countdownBrush, value);
    }

    public Brush AccentBrush
    {
        get => _accentBrush;
        private set => SetProperty(ref _accentBrush, value);
    }

    public void UpdateCountdown()
    {
        if (IsLive)
        {
            CountdownText = "● LIVE NOW";
            CountdownBrush = LiveBrush;
            AccentBrush = LiveBrush;
            return;
        }

        var remaining = StartsAt - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            CountdownText = "STARTING...";
            CountdownBrush = LiveBrush;
            AccentBrush = LiveBrush;
            return;
        }

        if (remaining.TotalDays >= 1)
        {
            CountdownText = $"{(int)remaining.TotalDays}d {remaining.Hours}h";
            CountdownBrush = MutedBrush;
            AccentBrush = MutedBrush;
        }
        else if (remaining.TotalHours >= 6)
        {
            CountdownText = $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            CountdownBrush = NormalBrush;
            AccentBrush = NormalBrush;
        }
        else if (remaining.TotalHours >= 1)
        {
            CountdownText = $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            CountdownBrush = WarnBrush;
            AccentBrush = WarnBrush;
        }
        else if (remaining.TotalMinutes >= 10)
        {
            CountdownText = $"{(int)remaining.TotalMinutes}m";
            CountdownBrush = UrgentBrush;
            AccentBrush = UrgentBrush;
        }
        else
        {
            CountdownText = $"{(int)remaining.TotalMinutes}m {remaining.Seconds:D2}s";
            CountdownBrush = CriticalBrush;
            AccentBrush = CriticalBrush;
        }
    }

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}
