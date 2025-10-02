using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Presentation.Views
{
    public partial class KillTracker : UserControl
    {
        private const int MaxKillCount = 3;

        private readonly ObservableCollection<KillEntity> _recentKills = new();
        private readonly IKillEventService _killEvents;
        private readonly IGameLogService _logService;
        private readonly ISettingsService _settings;
        private readonly ILogger<KillTracker> _logger;

        private int _fpsKillCount;
        private int _airKillCount;

        public KillTracker(
            IKillEventService killEvents,
            IGameLogService logService,
            ISettingsService settings,
            ILogger<KillTracker> logger)
        {
            InitializeComponent();

            _killEvents = killEvents ?? throw new ArgumentNullException(nameof(killEvents));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var view = CollectionViewSource.GetDefaultView(_recentKills);
            view.Filter = o =>
            {
                if (o is KillEntity kill)
                    return string.Equals(kill.Type, "FPS", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(kill.Type, "Air", StringComparison.OrdinalIgnoreCase);
                return false;
            };

            RecentKillsList.ItemsSource = view;
            _killEvents.KillReceived += OnKillReceived;

            TxtFpsKillCount.Text = "0";
            TxtAirKillCount.Text = "0";
            ConnectionStatus.Text = "Not Connected";

            try
            {
                if (_killEvents.LastKill != null)
                    OnKillReceived(_killEvents.LastKill);

                StartLogMonitor();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize KillTracker view.");
                ApplyConnectionStatus("Error initializing KillTracker", "Info");
            }
        }

        private void StartLogMonitor()
        {
            var path = _settings.GetSettings().GameLogFilePath?.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _logger.LogError("Invalid or missing game log path: {Path}", path);
                ApplyConnectionStatus("No log file", "Error");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _logService.StartAsync(path, CancellationToken.None);
                    _logger.LogInformation("GameLogService started for {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GameLogService crashed");
                    Dispatcher.Invoke(() => ApplyConnectionStatus("Error starting log monitor", "Error"));
                }
            });
        }

        private void OnKillReceived(KillEntity kill)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnKillReceived(kill));
                return;
            }

            // Handle Info/Status
            if (string.Equals(kill.Type, "Info", StringComparison.OrdinalIgnoreCase))
            {
                if (kill.Type != null) ApplyConnectionStatus(kill.Summary ?? "Info", kill.Type);
                LogStatusEntry(kill.Summary ?? "Info");
                return;
            }

            // Defensive: Only increment for valid types
            if (string.Equals(kill.Type, "FPS", StringComparison.OrdinalIgnoreCase))
                TxtFpsKillCount.Text = (++_fpsKillCount).ToString();
            else if (string.Equals(kill.Type, "Air", StringComparison.OrdinalIgnoreCase))
                TxtAirKillCount.Text = (++_airKillCount).ToString();

            // Add to recent kills list
            _recentKills.Insert(0, kill);
            if (_recentKills.Count > MaxKillCount)
                _recentKills.RemoveAt(_recentKills.Count - 1);
        }

        private void ApplyConnectionStatus(string summary, string type = "Info")
        {
            SolidColorBrush brush;
            try
            {
                if (string.Equals(summary, "Connected", StringComparison.OrdinalIgnoreCase))
                {
                    brush = new SolidColorBrush(Color.FromRgb(144, 238, 144));
                }
                else if (string.Equals(summary, "Disconnected", StringComparison.OrdinalIgnoreCase))
                {
                    brush = new SolidColorBrush(Color.FromRgb(240, 128, 128));
                }
                else
                {
                    var converterObj = TryFindResource("KillTypeToColorConverter");
                    if (converterObj is IValueConverter converter)
                        brush = (SolidColorBrush)converter.Convert(type, typeof(Brush), null, CultureInfo.CurrentCulture)!;
                    else
                        brush = new SolidColorBrush(Colors.Gray);
                }
            }
            catch (Exception ex)
            {
                brush = new SolidColorBrush(Colors.Gray);
                _logger.LogWarning(ex, "KillTypeToColorConverter failed for type={Type}", type);
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectionStatus.Text = summary;
                    ConnectionStatus.Foreground = brush;
                });
            }
            else
            {
                ConnectionStatus.Text = summary;
                ConnectionStatus.Foreground = brush;
            }
        }

        private void LogStatusEntry(string summary)
        {
            var entry = new KillEntity()
            {
                Timestamp = DateTime.Now,
                Type = "Info",
                Summary = summary
            };

            if (_recentKills.Count > 0 && _recentKills[0].Type == "Info")
                _recentKills.RemoveAt(0);

            _recentKills.Insert(0, entry);
        }
    }
}
