using System;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;
using System.Windows.Threading;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Presentation.Services;
using PackTracker.Presentation.Services.Admin;
using PackTracker.Presentation.Services.Navigation;
using PackTracker.Presentation.ViewModels;
using PackTracker.Presentation.Views.Admin;

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
            _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            int captionColor = 0x00252525;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        }

        #endregion

        #region Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly IUpdateService _updateService;
        private readonly AdminApiClient _adminApiClient;
        private readonly NavigationStateService _navigationState;
        private readonly DispatcherTimer _timer;
        private readonly ILogger<MainWindow> _logger;

        private UpdateInfo? _pendingUpdate;
        private bool _isAuthenticated;
        private DispatcherTimer? _toastTimer;
        private ImageSource? _currentUserAvatar;
        private string _discordDisplayName = "Not Logged In";
        private string _discordRank = "No Rank";
        private bool _canAccessAdmin;
        private string? _currentMainViewKey;
        private string? _currentAdminTier;

        #endregion

        #region Bindable Properties

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

        public bool CanAccessAdmin
        {
            get => _canAccessAdmin;
            set
            {
                if (_canAccessAdmin != value)
                {
                    _logger.LogInformation("CanAccessAdmin changed from {PreviousValue} to {NewValue}", _canAccessAdmin, value);
                    _canAccessAdmin = value;
                    OnPropertyChanged();
                }
                else
                {
                    _logger.LogInformation("CanAccessAdmin remained {Value}", value);
                }
            }
        }

        #endregion

        #region Constructor

        public MainWindow(
            IServiceProvider serviceProvider,
            ISettingsService settingsService,
            IUpdateService updateService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _adminApiClient = serviceProvider.GetRequiredService<AdminApiClient>();
            _navigationState = serviceProvider.GetRequiredService<NavigationStateService>();
            _logger = serviceProvider.GetRequiredService<ILogger<MainWindow>>();

            InitializeComponent();
            InitializeStaticVisualAssets();

            DataContext = this;
            WindowState = WindowState.Maximized;
            _logger.LogInformation(
                "MainWindow initialized. DataContextType={DataContextType}, AdminVisibilityBindingPresent={BindingPresent}",
                DataContext?.GetType().FullName ?? "<null>",
                BindingOperations.GetBindingExpressionBase(AdminNavButton, UIElement.VisibilityProperty) is not null);

            TxtTimeZone.Text = TimeZoneInfo.Local.StandardName;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) =>
            {
                TxtLocalTime.Text = DateTime.Now.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture) + "\n" +
                                    DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            };
            _timer.Start();

            NavigateToFirstView();

            Loaded += async (_, _) =>
            {
                await RefreshSidebarProfileAsync();
                await CheckForUpdateAsync();
                SubscribeToClaimNotifications();
            };
        }

        #endregion

        #region Navigation

        private void NavigateToFirstView()
        {
            _ = NavigateToFirstViewAsync();
        }

        private async Task NavigateToFirstViewAsync()
        {
            var settings = _settingsService.GetSettings();

            if (!settings.FirstRunComplete)
            {
                var welcomeView = _serviceProvider.GetRequiredService<WelcomeView>();
                ContentFrame.Navigate(welcomeView);
                return;
            }

            var jwtToken = settings.JwtToken;

            if (!string.IsNullOrWhiteSpace(jwtToken) && IsJwtValid(jwtToken))
            {
                NavigateToDashboard();
                return;
            }

            // Access token expired or missing — try the refresh token before forcing re-login
            var refreshToken = settings.JwtRefreshToken;
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                try
                {
                    // Wait for the embedded API to be ready before attempting the refresh
                    var apiProvider = _serviceProvider.GetRequiredService<IApiClientProvider>();
                    var apiReady = await WaitForApiAsync(apiProvider.BaseUrl);

                    if (apiReady)
                    {
                        using var client = apiProvider.CreateAnonymousClient();
                        var response = await client.PostAsJsonAsync("api/v1/auth/refresh",
                            new { RefreshToken = refreshToken });

                        if (response.IsSuccessStatusCode)
                        {
                            var payload = await response.Content.ReadFromJsonAsync<TokenPayload>();
                            if (payload is not null)
                            {
                                await _settingsService.UpdateSettingsAsync(s =>
                                {
                                    s.JwtToken = payload.access_token;
                                    s.JwtRefreshToken = payload.refresh_token;
                                });

                                NavigateToDashboard();
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                    logger?.LogWarning(ex, "Silent token refresh failed on startup.");
                }
            }

            NavigateToLogin();
        }

        private static async Task<bool> WaitForApiAsync(string baseUrl)
        {
            using var client = new System.Net.Http.HttpClient();
            var readinessUrl = $"{baseUrl.TrimEnd('/')}/health/ready";
            var livenessUrl = $"{baseUrl.TrimEnd('/')}/health";
            client.Timeout = TimeSpan.FromSeconds(2);
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var readinessResponse = await client.GetAsync(readinessUrl);
                    if (readinessResponse.IsSuccessStatusCode) return true;

                    if (readinessResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        var livenessResponse = await client.GetAsync(livenessUrl);
                        if (livenessResponse.IsSuccessStatusCode) return true;
                    }
                }
                catch { }
                await Task.Delay(500);
            }
            return false;
        }

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
                    case "Profile":
                        NavigateToProfile();
                        break;
                    case "Blueprints":
                        NavigateToBlueprintExplorer();
                        break;
                    case "CraftingQueue":
                        _ = NavigateToCraftingQueueAsync();
                        break;
                    case "ProcurementQueue":
                        NavigateToProcurementQueue();
                        break;
                    case "Settings":
                        NavigateToSettings();
                        break;
                    case "Admin":
                        NavigateToAdmin();
                        break;
                }
            }
        }

        internal void NavigateToDashboard()
        {
            _currentMainViewKey = "Dashboard";
            SetNavigationEnabled(true);
            var dashboardView = _serviceProvider.GetRequiredService<DashboardView>();
            ContentFrame.Navigate(dashboardView);
            _ = dashboardView.ViewModel.LoadCurrentUserAsync();
            _ = dashboardView.ViewModel.RefreshDataAsync();
            _ = RefreshSidebarProfileAsync();
        }

        private void NavigateToUex()
        {
            _currentMainViewKey = "TradingHub";
            var uexView = _serviceProvider.GetRequiredService<UexView>();
            ContentFrame.Navigate(uexView);
            _ = RefreshSidebarProfileAsync();
        }

        private void NavigateToRequestsHub()
        {
            _currentMainViewKey = "RequestHub";
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
            _currentMainViewKey = "Blueprints";
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
            _ = NavigateToCraftingQueueAsync();
        }

        public async Task NavigateToCraftingQueueAsync(Guid? requestId = null)
        {
            _currentMainViewKey = "CraftingQueue";
            try
            {
                var craftingView = _serviceProvider.GetRequiredService<CraftingRequestsView>();
                ContentFrame.Navigate(craftingView);

                if (craftingView.DataContext is CraftingRequestsViewModel vm)
                {
                    await vm.RefreshDataAsync();

                    if (requestId.HasValue)
                        vm.SelectedRequest = vm.Requests.FirstOrDefault(x => x.Id == requestId.Value);
                }
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
            _currentMainViewKey = "ProcurementQueue";
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
                        await NavigateToCraftingQueueAsync(request.Id);
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
            _currentMainViewKey = "Settings";
            try
            {
                var settingsView = _serviceProvider.GetRequiredService<SettingsView>();
                ContentFrame.Navigate(settingsView);
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Unable to resolve all required services for settings.",
                    "Dependency Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void NavigateToProfile()
        {
            _currentMainViewKey = "Profile";
            try
            {
                var profileView = _serviceProvider.GetRequiredService<ProfileView>();
                ContentFrame.Navigate(profileView);
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Unable to open the profile dossier.",
                    "Dependency Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void NavigateToAdmin()
        {
            if (!CanAccessAdmin)
            {
                return;
            }

            _navigationState.CaptureMainView(_currentMainViewKey ?? "Dashboard");
            var adminShell = _serviceProvider.GetRequiredService<AdminShellView>();
            adminShell.SetTierLabel(_currentAdminTier);
            ContentFrame.Navigate(adminShell);
        }

        internal void ReturnFromAdmin()
        {
            switch (_navigationState.LastMainViewKey)
            {
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
                    _ = NavigateToCraftingQueueAsync();
                    break;
                case "ProcurementQueue":
                    NavigateToProcurementQueue();
                    break;
                case "Settings":
                    NavigateToSettings();
                    break;
                case "Dashboard":
                default:
                    NavigateToDashboard();
                    break;
            }
        }

        #endregion

        #region Sidebar Controls

        private async void Exit_Click(object sender, RoutedEventArgs e)
        {
            await LogoutAsync();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            System.Windows.Application.Current.Shutdown();
        }

        private async Task LogoutAsync()
        {
            try
            {
                var settings = _settingsService.GetSettings();

                // Revoke refresh token on the server so it can't be reused after explicit logout
                if (!string.IsNullOrWhiteSpace(settings.JwtRefreshToken))
                {
                    try
                    {
                        var apiProvider = _serviceProvider.GetRequiredService<IApiClientProvider>();
                        using var client = apiProvider.CreateAnonymousClient();
                        await client.PostAsJsonAsync("api/v1/auth/logout",
                            new { RefreshToken = settings.JwtRefreshToken });
                    }
                    catch { /* best effort */ }
                }

                await _settingsService.UpdateSettingsAsync(s =>
                {
                    s.JwtToken = string.Empty;
                    s.JwtRefreshToken = string.Empty;
                    s.DiscordRefreshToken = string.Empty;
                });
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<ILogger<MainWindow>>();
                logger?.LogWarning(ex, "Failed to clear tokens on logout.");
            }
        }

        public async Task RefreshSidebarProfileAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var logger = scope.ServiceProvider.GetService<ILogger<MainWindow>>();

                var settings = _settingsService.GetSettings();
                var jwtToken = settings.JwtToken;

                if (string.IsNullOrWhiteSpace(jwtToken) || !IsJwtValid(jwtToken))
                {
                    await Dispatcher.InvokeAsync(SetDefaultProfile);
                    return;
                }

                var apiProvider = scope.ServiceProvider.GetRequiredService<IApiClientProvider>();
                var authTokenService = scope.ServiceProvider.GetRequiredService<AuthTokenService>();
                using var client = apiProvider.CreateClient();
                var response = await client.GetAsync("api/v1/profiles/me");
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    logger?.LogWarning("GET /api/v1/profiles/me returned 401. Attempting forced token refresh.");
                    await authTokenService.ForceRefreshAsync();
                    response.Dispose();
                    using var retryClient = apiProvider.CreateClient();
                    response = await retryClient.GetAsync("api/v1/profiles/me");
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger?.LogWarning("GET /api/v1/profiles/me returned {Status}", response.StatusCode);
                    await Dispatcher.InvokeAsync(SetDefaultProfile);
                    return;
                }

                var profile = await response.Content.ReadFromJsonAsync<Profile>();

                if (profile == null)
                {
                    logger?.LogWarning("No matching profile found for sidebar.");
                    await Dispatcher.InvokeAsync(SetDefaultProfile);
                    return;
                }

                // Download avatar bytes on background thread — BitmapImage can't fetch web URLs on the UI thread.
                byte[]? avatarBytes = null;
                var avatarUrl = profile.DiscordAvatarUrl;
                if (!string.IsNullOrWhiteSpace(avatarUrl) && Uri.TryCreate(avatarUrl, UriKind.Absolute, out _))
                {
                    try
                    {
                        using var avatarClient = new System.Net.Http.HttpClient();
                        avatarClient.Timeout = TimeSpan.FromSeconds(10);
                        avatarBytes = await avatarClient.GetByteArrayAsync(avatarUrl);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to download avatar from {Url}", avatarUrl);
                    }
                }

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
                    _logger.LogInformation(
                        "Sidebar profile resolved. ProfileId={ProfileId}, DiscordId={DiscordId}, DiscordRank={DiscordRank}, DisplayName={DisplayName}",
                        profile.Id,
                        profile.DiscordId,
                        DiscordRank,
                        DiscordDisplayName);

                    if (avatarBytes != null)
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.StreamSource = new System.IO.MemoryStream(avatarBytes);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
                            CurrentUserAvatar = bmp;
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Failed to create BitmapImage from avatar bytes.");
                            CurrentUserAvatar = LoadFallbackAvatar();
                        }
                    }
                    else
                    {
                        CurrentUserAvatar = LoadFallbackAvatar();
                    }

                    SidebarAvatarImage.Source = CurrentUserAvatar;

                    // Division patch takes priority; fall back to user's selected theme, then HW icon.
                    var key = profile.DiscordDivision?.Trim().ToLowerInvariant()
                              ?? _settingsService.GetSettings().Theme?.Trim().ToLowerInvariant();
                    var patchAsset = key switch
                    {
                        "tacops" => "Assets/tacops.png",
                        "specops" => "Assets/specops.png",
                        "locops" => "Assets/locops.png",
                        "arcops" => "Assets/arcops.png",
                        _ => "Assets/HWiconnew.png"
                    };
                });

                await RefreshAdminAccessAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh sidebar profile.");
                await Dispatcher.InvokeAsync(SetDefaultProfile);
            }
        }

        private async Task RefreshAdminAccessAsync()
        {
            var apiProvider = _serviceProvider.GetRequiredService<IApiClientProvider>();
            var jwtIdentity = ExtractJwtIdentity(_settingsService.GetSettings().JwtToken);
            _logger.LogInformation(
                "Admin access refresh started. DiscordDisplayName={DiscordDisplayName}, DiscordRank={DiscordRank}, JwtDisplayName={JwtDisplayName}, JwtUsername={JwtUsername}, DataContextType={DataContextType}",
                DiscordDisplayName,
                DiscordRank,
                jwtIdentity.DisplayName ?? "<null>",
                jwtIdentity.Username ?? "<null>",
                DataContext?.GetType().FullName ?? "<null>");

            try
            {
                var apiReady = await WaitForApiAsync(apiProvider.BaseUrl);
                _logger.LogInformation("Admin access API readiness: {ApiReady}", apiReady);
                if (!apiReady)
                {
                    CanAccessAdmin = false;
                    _currentAdminTier = null;
                    _logger.LogWarning("Admin access refresh stopped because API readiness failed.");
                    return;
                }

                var access = await _adminApiClient.GetAccessAsync();
                CanAccessAdmin = access?.CanAccessAdmin == true;
                _currentAdminTier = access?.HighestTier;
                _logger.LogInformation(
                    "Admin access refresh result. ReturnedCanAccessAdmin={ReturnedCanAccessAdmin}, EffectiveCanAccessAdmin={EffectiveCanAccessAdmin}, HighestTier={HighestTier}",
                    access?.CanAccessAdmin,
                    CanAccessAdmin,
                    _currentAdminTier ?? "<null>");
            }
            catch (Exception ex)
            {
                CanAccessAdmin = false;
                _currentAdminTier = null;
                _logger.LogError(ex, "Admin access refresh threw an exception.");
            }

            await Dispatcher.InvokeAsync(() =>
            {
                BindingOperations.GetBindingExpressionBase(
                    AdminNavButton,
                    UIElement.VisibilityProperty)?.UpdateTarget();
                _logger.LogInformation(
                    "AdminNavButton binding refreshed. Visibility={Visibility}, CanAccessAdmin={CanAccessAdmin}, IsAuthenticated={IsAuthenticated}",
                    AdminNavButton.Visibility,
                    CanAccessAdmin,
                    _isAuthenticated);
            });
        }

        private (string? DisplayName, string? Username) ExtractJwtIdentity(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (null, null);
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                string? displayName =
                    jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ??
                    jwt.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value ??
                    jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;

                string? username =
                    jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ??
                    jwt.Claims.FirstOrDefault(c => c.Type == "username")?.Value ??
                    jwt.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;

                return (displayName?.Trim(), username?.Trim());
            }
            catch
            {
                return (null, null);
            }
        }

        private void SetDefaultProfile()
        {
            DiscordDisplayName = "Not Logged In";
            DiscordRank = "No Rank";
            CurrentUserAvatar = LoadFallbackAvatar();
            SidebarAvatarImage.Source = CurrentUserAvatar;
            CanAccessAdmin = false;
            _currentAdminTier = null;
            BindingOperations.GetBindingExpressionBase(
                AdminNavButton,
                UIElement.VisibilityProperty)?.UpdateTarget();
            _logger.LogInformation("Sidebar profile reset. AdminNavButton visibility is {Visibility}", AdminNavButton.Visibility);
        }

        private ImageSource LoadAvatar(string? url, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                return LoadFallbackAvatar();

            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    logger?.LogWarning("Invalid Discord avatar URL: {AvatarUrl}", url);
                    return LoadFallbackAvatar();
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load Discord avatar from {AvatarUrl}", url);
                return LoadFallbackAvatar();
            }
        }

        private ImageSource LoadFallbackAvatar()
        {
            try
            {
                return LoadPackImage("Assets/HWiconnew.png") as ImageSource ?? new DrawingImage();
            }
            catch
            {
                return new DrawingImage();
            }
        }

        private void InitializeStaticVisualAssets()
        {
            Icon = LoadPackImage("Assets/housewolf2.ico");
            MainBackgroundImage.Source = LoadPackImage("Assets/Pack_Tracker.png");
         
        }

        private ImageSource? ResolveSidebarThemeImage()
        {
            var theme = _settingsService.GetSettings().Theme;
            var assetPath = theme?.Trim().ToLowerInvariant() switch
            {
                "tacops" => "Assets/tacops.png",
                "specops" => "Assets/specops.png",
                "locops" => "Assets/locops.png",
                "arcops" => "Assets/arcops.png",
                _ => "Assets/HWiconnew.png"
            };

            return LoadPackImage(assetPath);
        }

        private static BitmapImage? LoadPackImage(string relativeAssetPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativeAssetPath);

            try
            {
                var uri = new Uri($"pack://application:,,,/{relativeAssetPath}", UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                try
                {
                    var fullPath = Path.Combine(AppContext.BaseDirectory, relativeAssetPath.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(fullPath))
                    {
                        return null;
                    }

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
        }

        #endregion

        #region Claim Notifications

        private void SubscribeToClaimNotifications()
        {
            var signalR = _serviceProvider.GetService<SignalRChatService>();
            if (signalR is null) return;

            signalR.RequestClaimed += dto => ShowClaimToast(
                "Request Claimed",
                $"{dto.ClaimerDisplayName} claimed your {dto.RequestType} request: {dto.RequestLabel}");

            signalR.ClaimConfirmed += dto => ShowClaimToast(
                "Claim Confirmed",
                $"You claimed {dto.RequestType} request: {dto.RequestLabel}");
        }

        private void ShowClaimToast(string title, string message)
        {
            Dispatcher.Invoke(() =>
            {
                ClaimToastTitle.Text = title;
                ClaimToastMessage.Text = message;
                ClaimToastPanel.Visibility = Visibility.Visible;

                _toastTimer?.Stop();
                _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                _toastTimer.Tick += (_, _) =>
                {
                    _toastTimer!.Stop();
                    ClaimToastPanel.Visibility = Visibility.Collapsed;
                };
                _toastTimer.Start();
            });
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
