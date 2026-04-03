using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Events;
using PackTracker.Infrastructure.Events;
using PackTracker.Infrastructure.Services;

namespace PackTracker.Presentation.Views;

/// <summary>
/// Transparent overlay window for displaying kill feed over Star Citizen.
/// </summary>
/// <remarks>
/// Features:
/// - Topmost, transparent, borderless window
/// - Draggable by clicking anywhere on the window
/// - Shows FPS and Air kill counts
/// - Displays recent kills in real-time
/// - Subscribes to PackTrackerEventDispatcher for kill events
/// </remarks>
public partial class KillFeedOverlay : Window
{
    private const int MaxRecentKills = 5;

    private readonly ObservableCollection<KillDisplayItem> _recentKills = new();
    private readonly ILogger<KillFeedOverlay>? _logger;

    private int _fpsKillCount;
    private int _airKillCount;

    public KillFeedOverlay(ILogger<KillFeedOverlay>? logger = null)
    {
        InitializeComponent();

        _logger = logger;

        RecentKillsList.ItemsSource = _recentKills;

        // Subscribe to kill events
        PackTrackerEventDispatcher.ActorDeathEvent += OnActorDeathEvent;

        _logger?.LogInformation("Kill feed overlay initialized");
    }

    /// <summary>
    /// Handles actor death events from the event dispatcher.
    /// </summary>
    private void OnActorDeathEvent(ActorDeathData data)
    {
        // Skip if not in real-time mode (prevents historical spam)
        if (!GameLogService.IsReady)
            return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnActorDeathEvent(data));
            return;
        }

        try
        {
            // Update kill counters
            switch (data.ClassifiedType)
            {
                case Domain.Enums.KillType.FPS:
                    TxtFpsKills.Text = (++_fpsKillCount).ToString();
                    break;
                case Domain.Enums.KillType.AIR:
                    TxtAirKills.Text = (++_airKillCount).ToString();
                    break;
            }

            // Add to recent kills list
            var killItem = new KillDisplayItem
            {
                Type = data.ClassifiedType.ToString(),
                TypeColor = data.ClassifiedType == Domain.Enums.KillType.FPS
                    ? new SolidColorBrush(Color.FromRgb(255, 68, 68))  // Red for FPS
                    : new SolidColorBrush(Color.FromRgb(68, 136, 255)), // Blue for Air
                Attacker = data.AttackerPilot,
                Target = data.VictimPilot,
                Weapon = data.WeaponClass,
                Timestamp = data.Timestamp
            };

            _recentKills.Insert(0, killItem);

            // Keep only the most recent kills
            while (_recentKills.Count > MaxRecentKills)
            {
                _recentKills.RemoveAt(_recentKills.Count - 1);
            }

            _logger?.LogDebug("Kill event displayed in overlay: {Type} - {Attacker} -> {Target}",
                data.ClassifiedType, data.AttackerPilot, data.VictimPilot);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing kill event in overlay");
        }
    }

    /// <summary>
    /// Allows dragging the window by clicking anywhere on it.
    /// </summary>
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove can throw if the window is being closed
            }
        }
    }

    /// <summary>
    /// Closes the overlay window.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Clean up event subscriptions when window closes.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Unsubscribe from events to prevent memory leaks
        PackTrackerEventDispatcher.ActorDeathEvent -= OnActorDeathEvent;

        _logger?.LogInformation("Kill feed overlay closed");
    }
}

/// <summary>
/// Display model for kill items in the overlay.
/// </summary>
public class KillDisplayItem
{
    public string Type { get; set; } = string.Empty;
    public SolidColorBrush TypeColor { get; set; } = Brushes.Gray;
    public string Attacker { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Weapon { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
