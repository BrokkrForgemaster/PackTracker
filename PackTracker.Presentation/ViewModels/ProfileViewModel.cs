using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Profiles;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public sealed class ProfileViewModel : ViewModelBase
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly ILogger<ProfileViewModel> _logger;

    private Guid _profileId;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string _discordRank = string.Empty;
    private string _discordDivision = string.Empty;
    private string _discordAvatarUrl = string.Empty;
    private string _showcaseImageUrl = string.Empty;
    private ImageSource? _showcaseImageSource;
    private string _showcaseEyebrow = string.Empty;
    private string _showcaseTagline = string.Empty;
    private string _showcaseBio = string.Empty;
    private string _statusMessage = "Load your profile to begin.";
    private bool _isBusy;

    public ObservableCollection<CurrentProfileMedalDto> Medals { get; } = new();
    public ObservableCollection<CurrentProfileMedalDto> DisplayMedals { get; } = new();
    public ObservableCollection<CurrentProfileMedalDto> DisplayRibbons { get; } = new();
    public ICommand OpenHouseWolfWebsiteCommand { get; }

    public ProfileViewModel(
        IApiClientProvider apiClientProvider,
        ILogger<ProfileViewModel> logger)
    {
        _apiClientProvider = apiClientProvider;
        _logger = logger;

        OpenHouseWolfWebsiteCommand = new RelayCommand(OpenHouseWolfWebsite);
    }

    public Guid ProfileId
    {
        get => _profileId;
        set => SetProperty(ref _profileId, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
                OnPropertyChanged(nameof(PreviewDisplayName));
        }
    }

    public string DiscordRank
    {
        get => _discordRank;
        set
        {
            if (SetProperty(ref _discordRank, value))
                OnPropertyChanged(nameof(PreviewRoleLine));
        }
    }

    public string DiscordDivision
    {
        get => _discordDivision;
        set
        {
            if (SetProperty(ref _discordDivision, value))
                OnPropertyChanged(nameof(PreviewRoleLine));
        }
    }

    public string DiscordAvatarUrl
    {
        get => _discordAvatarUrl;
        set
        {
            if (SetProperty(ref _discordAvatarUrl, value))
            {
                RefreshPreviewImage();
                OnPropertyChanged(nameof(PreviewImageUrl));
            }
        }
    }

    public string ShowcaseImageUrl
    {
        get => _showcaseImageUrl;
        set
        {
            if (SetProperty(ref _showcaseImageUrl, value))
            {
                RefreshPreviewImage();
                OnPropertyChanged(nameof(PreviewImageUrl));
            }
        }
    }

    public ImageSource? ShowcaseImageSource
    {
        get => _showcaseImageSource;
        private set => SetProperty(ref _showcaseImageSource, value);
    }

    public string ShowcaseEyebrow
    {
        get => _showcaseEyebrow;
        set => SetProperty(ref _showcaseEyebrow, value);
    }

    public string ShowcaseTagline
    {
        get => _showcaseTagline;
        set => SetProperty(ref _showcaseTagline, value);
    }

    public string ShowcaseBio
    {
        get => _showcaseBio;
        set => SetProperty(ref _showcaseBio, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string PreviewDisplayName =>
        !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Username;

    public string PreviewImageUrl =>
        !string.IsNullOrWhiteSpace(ShowcaseImageUrl) ? ShowcaseImageUrl : DiscordAvatarUrl;

    public string PreviewRoleLine =>
        string.IsNullOrWhiteSpace(DiscordDivision)
            ? DiscordRank
            : $"{DiscordRank} • {DiscordDivision}";

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = "Loading profile...";

        try
        {
            using var client = _apiClientProvider.CreateClient();
            var dto = await client.GetFromJsonAsync<CurrentProfileDto>("api/v1/profiles/me", ct);

            if (dto is null)
            {
                StatusMessage = "Unable to load your profile.";
                return;
            }

            Apply(dto);

            StatusMessage = dto.Medals.Count == 0
                ? "Profile synchronized."
                : $"Profile synchronized. {dto.Medals.Count} award(s) on record.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile data");
            StatusMessage = $"Unable to load profile: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(CurrentProfileDto dto)
    {
        ProfileId = dto.Id;
        Username = dto.Username;
        DisplayName = dto.DiscordDisplayName ?? dto.Username;
        DiscordRank = dto.DiscordRank ?? "Foundling";
        DiscordDivision = dto.DiscordDivision ?? string.Empty;
        DiscordAvatarUrl = NormalizeUrl(dto.DiscordAvatarUrl);
        ShowcaseImageUrl = NormalizeUrl(dto.ShowcaseImageUrl);

        ShowcaseEyebrow = dto.ShowcaseEyebrow ?? InferEyebrow(dto.DiscordDivision);
        ShowcaseTagline = dto.ShowcaseTagline ?? InferTagline(dto.DiscordRank, dto.DiscordDivision);
        ShowcaseBio = dto.ShowcaseBio ?? string.Empty;

        Medals.Clear();
        DisplayMedals.Clear();
        DisplayRibbons.Clear();

        foreach (var award in dto.Medals)
        {
            Medals.Add(award);

            if (string.Equals(award.AwardType, "Ribbon", StringComparison.OrdinalIgnoreCase))
            {
                DisplayRibbons.Add(award);
            }
            else
            {
                DisplayMedals.Add(award);
            }
        }

        RefreshPreviewImage();

        OnPropertyChanged(nameof(PreviewDisplayName));
        OnPropertyChanged(nameof(PreviewImageUrl));
        OnPropertyChanged(nameof(PreviewRoleLine));
    }

    private void RefreshPreviewImage()
    {
        var preferredImage = !string.IsNullOrWhiteSpace(ShowcaseImageUrl)
            ? ShowcaseImageUrl
            : DiscordAvatarUrl;

        _logger.LogInformation("Refreshing preview image. Preferred: {Preferred}", preferredImage);
        ShowcaseImageSource = BuildImageSource(preferredImage);
    }

    private string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        url = url.Trim();

        // 1. Full URLs
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        // 2. Data URIs
        if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            return url;

        // 3. Local Files (only if we are sure it's a path)
        if (url.Contains(":\\") || url.StartsWith("\\\\") || (url.Contains("/") && File.Exists(url)))
            return url;

        const string host = "housewolf.co";
        const string baseUrl = "https://www.housewolf.co";

        // 4. Domain-only URLs
        if (url.StartsWith(host, StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("www." + host, StringComparison.OrdinalIgnoreCase))
            return $"https://{url.TrimStart('/')}";

        // 5. Relative paths for HouseWolf
        var path = url.TrimStart('/');
        return $"{baseUrl}/{path}";
    }

    private ImageSource? BuildImageSource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("Cannot build image source: URL is empty.");
            return null;
        }

        try
        {
            _logger.LogInformation("Attempting to load image from: {Url}", value);
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            
            // OnLoad ensures the image is fully downloaded/decoded before EndInit returns,
            // which is important for freezing and thread safety.
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            
            // IgnoreImageCache prevents stale images if the URL is updated on the server.
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                bitmap.UriSource = uri;
            }
            else
            {
                // Fallback for relative paths that slipped through
                bitmap.UriSource = new Uri(value, UriKind.RelativeOrAbsolute);
            }

            bitmap.EndInit();

            if (bitmap.CanFreeze)
                bitmap.Freeze();

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FAILED to load profile image from {Url}", value);
            
            // If the showcase image failed and we haven't tried the Discord avatar yet, try it.
            if (value != DiscordAvatarUrl && !string.IsNullOrWhiteSpace(DiscordAvatarUrl))
            {
                _logger.LogInformation("Falling back to Discord avatar: {AvatarUrl}", DiscordAvatarUrl);
                return BuildImageSource(DiscordAvatarUrl);
            }

            return null;
        }
    }

    private void OpenHouseWolfWebsite()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.housewolf.co")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open HouseWolf website");
        }
    }

    private static string InferEyebrow(string? division) =>
        string.IsNullOrWhiteSpace(division)
            ? "HOUSE WOLF"
            : $"{division.ToUpperInvariant()} CORE";

    private static string InferTagline(string? rank, string? division)
    {
        var resolvedRank = string.IsNullOrWhiteSpace(rank) ? "House Wolf" : rank;
        return string.IsNullOrWhiteSpace(division)
            ? resolvedRank
            : $"{resolvedRank} • {division}";
    }
}