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
    private readonly IHouseWolfProfileService _houseWolfProfileService;
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
        IHouseWolfProfileService houseWolfProfileService,
        ILogger<ProfileViewModel> logger)
    {
        _apiClientProvider = apiClientProvider;
        _houseWolfProfileService = houseWolfProfileService;
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

            try
            {
                var hwProfile = await _houseWolfProfileService.GetProfileByDiscordIdAsync(dto.DiscordId); ;

                if (hwProfile is not null)
                {
                    if (!string.IsNullOrWhiteSpace(hwProfile.ImageUrl))
                    {
                        var houseWolfImage = NormalizeUrl(hwProfile.ImageUrl);

                        if (!string.IsNullOrWhiteSpace(houseWolfImage))
                            ShowcaseImageUrl = houseWolfImage;
                    }

                    if (!string.IsNullOrWhiteSpace(hwProfile.SubDivision))
                        ShowcaseEyebrow = hwProfile.SubDivision;

                    if (!string.IsNullOrWhiteSpace(hwProfile.Bio))
                        ShowcaseBio = hwProfile.Bio;

                    if (!string.IsNullOrWhiteSpace(hwProfile.CharacterName))
                        DisplayName = hwProfile.CharacterName;

                    if (!string.IsNullOrWhiteSpace(hwProfile.Division))
                        DiscordDivision = hwProfile.Division;

                    RefreshPreviewImage();

                    _logger.LogInformation("FINAL PROFILE IMAGE URL: {Final}", ShowcaseImageUrl);
                    _logger.LogInformation("FINAL PROFILE IMAGE SOURCE SET: {HasImage}", ShowcaseImageSource is not null);

                    StatusMessage = dto.Medals.Count == 0
                        ? "Profile synced with housewolf.co."
                        : $"Profile synced with housewolf.co. {dto.Medals.Count} award(s) on record.";
                }
                else
                {
                    StatusMessage = "Profile loaded. No HouseWolf character profile found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HouseWolf database connection failed.");
                StatusMessage = "Profile loaded locally. (HouseWolf Offline)";
            }
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

        ShowcaseImageSource = BuildImageSource(preferredImage);
    }

    private string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        url = url.Trim();

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            return url;

        if (File.Exists(url))
            return url;

        if (url.StartsWith("/"))
        {
            const string baseUrl = "https://www.housewolf.co";
            return $"{baseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
        }

        return url;
    }

    private ImageSource? BuildImageSource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            if (value.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = value.IndexOf(',');
                if (commaIndex < 0)
                    return null;

                var base64 = value[(commaIndex + 1)..];
                var bytes = Convert.FromBase64String(base64);

                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();

                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();

                return image;
            }

            if (File.Exists(value))
            {
                var image = new BitmapImage();

                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(value, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                return image;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                var image = new BitmapImage();

                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = uri;
                image.EndInit();
                image.Freeze();

                return image;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build profile image source.");
        }

        return null;
    }

    private void OpenHouseWolfWebsite()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.housewolf.co/profile")
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