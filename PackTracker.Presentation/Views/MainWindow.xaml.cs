using System;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views
{
    /// <summary>
    /// Main application window with navigation, sidebar profile rendering, clock display,
    /// and update notification logic.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region DWM title bar

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            int dark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            int captionColor = 0x00252525;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        }

        #endregion

        #region Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly IUpdateService _updateService;
        private readonly AppDbContext _dbContext;
        private readonly DispatcherTimer _timer;

        private UpdateInfo? _pendingUpdate;
        private bool _isAuthenticated;
        private ImageSource? _currentUserAvatar;
        private string _discordDisplayName = "Not Logged In";
        private string _discordRank = "No Rank";
        #endregion

        #region Bindable Properties

        /// <summary>
        /// Gets or sets the current user's avatar image displayed in the sidebar.
        /// </summary>
        public ImageSource? CurrentUserAvatar
        {
            get => _currentUserAvatar;
            set
            {
                if (!Equals(_currentUserAvatar, value))
                {
                    _currentUserAvatar = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current user's Discord display name shown in the sidebar.
        /// </summary>
        public string DiscordDisplayName
        {
            get => _discordDisplayName;
            set
            {
                if (_discordDisplayName != value)
                {
                    _discordDisplayName = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current user's Discord rank or role shown in the sidebar.
        /// </summary>
        public string DiscordRank
        {
            get => _discordRank;
            set
            {
                if (_discordRank != value)
                {
                    _discordRank = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow(
            IServiceProvider serviceProvider,
            ISettingsService settingsService,
            IUpdateService updateService,
            AppDbContext dbContext)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

            InitializeComponent();

            DataContext = this;
            WindowState = WindowState.Maximized;

            TxtTimeZone.Text = TimeZoneInfo.Local.StandardName;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) =>
            {
                TxtLocalTime.Text = DateTime.Now.ToString("MMMM d, yyyy") + "\n" +
                                    DateTime.Now.ToString("HH:mm:ss");
            };
            _timer.Start();

            NavigateToFirstView();

            _ = RefreshSidebarProfileAsync();
            _ = CheckForUpdateAsync();
        }

        #endregion

        #region Navigation

        private void NavigateToFirstView()
        {
            var settings = _settingsService.GetSettings();

            if (!settings.FirstRunComplete)
            {
                NavigateToSettings();
                return;
            }

            var jwtToken = settings.JwtToken;

            if (string.IsNullOrWhiteSpace(jwtToken) || !IsJwtValid(jwtToken))
            {
                NavigateToLogin();
                return;
            }

            NavigateToDashboard();
        }

        /// <summary>
        /// Validates JWT structure and expiration without throwing.
        /// </summary>
        private bool IsJwtValid(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Count(c => c == '.') != 2)
                return false;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                var expiresUtc = jwt.ValidTo;
                if (expiresUtc == default)
                    return false;

                return expiresUtc > DateTime.UtcNow;
            }
            catch
            {
                return false;
            }
        }

        private void SetNavigationEnabled(bool enabled)
        {
            _isAuthenticated = enabled;

            foreach (UIElement child in NavButtonPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.IsEnabled = enabled;
                    btn.Opacity = enabled ? 1.0 : 0.35;
                }
            }
        }

        private void NavigateToLogin()
        {
            SetNavigationEnabled(false);
            var loginView = _serviceProvider.GetRequiredService<LoginView>();
            ContentFrame.Navigate(loginView);
        }

        private void Navigate_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAuthenticated)
                return;

            if (sender is Button btn && btn.Tag is string tag)
            {
                switch (tag)
                {
                    case "Dashboard":
                        NavigateToDashboard();
                        break;
                    case "TradingHub":
                        NavigateToUex();
                        break;
                    case "RequestHub":
                        NavigateToRequestsHub();
                        break;
                    case "Blueprints":
                        NavigateToBlueprintExplorer();
                        break;
                    case "CraftingQueue":
                        NavigateToCraftingQueue();
                        break;
                    case "ProcurementQueue":
                        NavigateToProcurementQueue();
                        break;
                    case "Settings":
                        NavigateToSettings();
                        break;
                }
            }
        }

        internal void NavigateToDashboard()
        {
            SetNavigationEnabled(true);
            var dashboardView = _serviceProvider.GetRequiredService<DashboardView>();
            ContentFrame.Navigate(dashboardView);
            _ = RefreshSidebarProfileAsync();
        }

        private void NavigateToUex()
        {
            var uexView = _serviceProvider.GetRequiredService<UexView>();
            ContentFrame.Navigate(uexView);
            _ = RefreshSidebarProfileAsync();
        }

        private void NavigateToRequestsHub()
        {
            try
            {
                var requestView = _serviceProvider.GetRequiredService<RequestsView>();
                ContentFrame.Navigate(requestView);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to navigate to Requests Hub");

                MessageBox.Show(
                    $"Failed to open Requests Hub:\n{ex}",
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void NavigateToBlueprintExplorer()
        {
            try
            {
                var blueprintView = _serviceProvider.GetRequiredService<BlueprintExplorerView>();
                ContentFrame.Navigate(blueprintView);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to navigate to Blueprint Explorer");

                MessageBox.Show(
                    $"Failed to open Blueprint Explorer:\n{ex}",
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void NavigateToCraftingQueue()
        {
            try
            {
                var craftingView = _serviceProvider.GetRequiredService<CraftingRequestsView>();
                ContentFrame.Navigate(craftingView);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to navigate to Crafting Queue");

                MessageBox.Show(
                    $"Failed to open Crafting Queue:\n{ex}",
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void NavigateToProcurementQueue()
        {
            try
            {
                var procurementView = _serviceProvider.GetRequiredService<ProcurementRequestsView>();
                ContentFrame.Navigate(procurementView);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to navigate to Procurement Queue");

                MessageBox.Show(
                    $"Failed to open Procurement Queue:\n{ex}",
                    "Navigation Error",
                    MessageBoxButton.OK,
                MessageBoxImage.Error);
            }
        }

        internal async Task NavigateToActiveRequestAsync(ActiveRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            switch (request.RequestType.Trim())
            {
                case "Crafting":
                    {
                        var view = _serviceProvider.GetRequiredService<CraftingRequestsView>();
                        ContentFrame.Navigate(view);
                        if (view.DataContext is CraftingRequestsViewModel vm)
                        {
                            await vm.RefreshDataAsync();
                            vm.SelectedRequest = vm.Requests.FirstOrDefault(x => x.Id == request.Id);
                        }
                        break;
                    }
                case "Procurement":
                    {
                        var view = _serviceProvider.GetRequiredService<ProcurementRequestsView>();
                        ContentFrame.Navigate(view);
                        if (view.DataContext is ProcurementRequestsViewModel vm)
                        {
                            await vm.RefreshDataAsync();
                            vm.SelectedRequest = vm.Requests.FirstOrDefault(x => x.Id == request.Id);
                        }
                        break;
                    }
                default:
                    {
                        var view = _serviceProvider.GetRequiredService<RequestsView>();
                        ContentFrame.Navigate(view);
                        if (view.DataContext is RequestsViewModel vm)
                        {
                            await vm.RefreshAsync();
                            vm.SelectedRequest = vm.Requests.FirstOrDefault(x => x.Id == request.Id);
                        }
                        break;
                    }
            }
        }

        private void NavigateToSettings()
        {
            var themeManager = _serviceProvider.GetService<IThemeManager>();
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();

            if (themeManager is not null && loggerFactory is not null)
            {
                var logger = loggerFactory.CreateLogger<SettingsView>();
                var settingsView = new SettingsView(_serviceProvider, _settingsService, themeManager, logger);
                ContentFrame.Navigate(settingsView);
            }
            else
            {
                MessageBox.Show(
                    "Unable to resolve all required services for settings.",
                    "Dependency Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Sidebar Controls

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Refreshes the sidebar profile card using the most recently logged-in profile.
        /// </summary>
        public async Task RefreshSidebarProfileAsync()
        {
            try
            {
                // We use a fresh scope/context to ensure we see the latest data from the database
                // after the PackTracker API has updated it.
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var profile = await db.Profiles
                    .OrderByDescending(p => p.LastLogin)
                    .FirstOrDefaultAsync();

                if (profile == null)
                {
                    await Dispatcher.InvokeAsync(SetDefaultProfile);
                    return;
                }

                var avatar = profile.DiscordAvatarUrl;

                await Dispatcher.InvokeAsync(() =>
                {
                    DiscordDisplayName =
                        !string.IsNullOrWhiteSpace(profile.DiscordDisplayName)
                            ? profile.DiscordDisplayName
                            : !string.IsNullOrWhiteSpace(profile.Username)
                                ? profile.Username
                                : "Unknown User";
                    
                    DiscordRank = !string.IsNullOrWhiteSpace(profile.DiscordRank)
                        ? profile.DiscordRank
                        : "No Rank";

                    // DiscordRank =  profile.DiscordRank.OrderByDescending<char, object>(r => r.Position).FirstOrDefault(); ?? "No Rank";

                    CurrentUserAvatar = LoadAvatar(avatar);

                    SidebarAvatarImage.Source = CurrentUserAvatar;
                });
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogWarning(ex, "Failed to refresh sidebar profile.");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(SetDefaultProfile);
            }
        }

        private void SetDefaultProfile()
        {
            DiscordDisplayName = "Not Logged In";
            DiscordRank = "No Rank";
            CurrentUserAvatar = LoadFallbackAvatar();
        }

        private ImageSource LoadAvatar(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return LoadFallbackAvatar();

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return LoadFallbackAvatar();
            }
        }

        private ImageSource LoadFallbackAvatar()
        {
            try
            {
                return new BitmapImage(new Uri("pack://application:,,,/Assets/HWiconnew.png"));
            }
            catch
            {
                // Absolute last resort - empty transparent image
                return new DrawingImage();
            }
        }


        #endregion

        #region Auto-Update

        private async Task CheckForUpdateAsync()
        {
            try
            {
                var update = await _updateService.CheckForUpdateAsync(CancellationToken.None);
                if (update is not null)
                    Dispatcher.Invoke(() => ShowUpdateAvailable(update));
            }
            catch
            {
                // Best effort only.
            }
        }

        private void ShowUpdateAvailable(UpdateInfo update)
        {
            _pendingUpdate = update;

            PostureTitle.Text = "UPDATE AVAILABLE";
            PostureTitle.Foreground = Brushes.LightGreen;

            UpdateVersionLabel.Text = $"v{update.Version}  ·  {update.PublishedAt:MMM d, yyyy}";
            UpdateNotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                ? "No release notes provided."
                : update.ReleaseNotes.Trim();

            NormalPostureContent.Visibility = Visibility.Collapsed;
            UpdatePostureContent.Visibility = Visibility.Visible;

            // Show a modal dialog so users on smaller screens can still install the update
            var notes = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                ? string.Empty
                : $"\n\n{update.ReleaseNotes.Trim()}";
            var result = MessageBox.Show(
                $"PackTracker v{update.Version} is available.{notes}\n\nDownload and install now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                DownloadUpdate_Click(this, new RoutedEventArgs());
        }

        private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate is null)
                return;

            DownloadUpdateButton.IsEnabled = false;
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadStatusText.Visibility = Visibility.Visible;
            DownloadStatusText.Text = "Downloading…";

            try
            {
                var progress = new Progress<int>(pct =>
                {
                    DownloadProgressBar.Value = pct;
                    DownloadStatusText.Text = $"Downloading… {pct}%";
                });

                var filePath = await _updateService.DownloadUpdateAsync(
                    _pendingUpdate,
                    progress,
                    CancellationToken.None);

                DownloadStatusText.Text = "Download complete. Launching installer…";
                await Task.Delay(800);

                await _updateService.InstallAndRestartAsync(filePath);
            }
            catch (Exception ex)
            {
                DownloadStatusText.Text = $"Download failed: {ex.Message}";
                DownloadUpdateButton.IsEnabled = true;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Help System

        private HelpView? _helpView;

        public void ShowHelp(string sectionName, string anchor)
        {
            if (_helpView == null)
            {
                _helpView = new HelpView();
                HelpContentControl.Content = _helpView;
            }

            _helpView.NavigateToSection(sectionName, anchor);
            HelpOverlay.Visibility = Visibility.Visible;
        }

        public void HideHelp()
        {
            HelpOverlay.Visibility = Visibility.Collapsed;
        }

        private void HelpOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Only hide if the user clicks the dark background (HelpOverlay)
            // and not the HelpPanel itself.
            if (e.OriginalSource == HelpOverlay)
            {
                HideHelp();
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
