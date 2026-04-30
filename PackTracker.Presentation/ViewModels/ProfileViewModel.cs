using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Json;
using PackTracker.Application.DTOs.Profiles;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public sealed class ProfileViewModel : ViewModelBase
{
    private readonly IApiClientProvider _apiClientProvider;

    private Guid _profileId;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string _discordRank = string.Empty;
    private string _discordDivision = string.Empty;
    private string _discordAvatarUrl = string.Empty;
    private string _showcaseImageUrl = string.Empty;
    private string _showcaseEyebrow = string.Empty;
    private string _showcaseTagline = string.Empty;
    private string _showcaseBio = string.Empty;
    private string _statusMessage = "Load your profile to begin.";
    private bool _isBusy;

    public ObservableCollection<CurrentProfileMedalDto> Medals { get; } = new();

    public ProfileViewModel(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
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
            {
                OnPropertyChanged(nameof(PreviewDisplayName));
            }
        }
    }

    public string DiscordRank
    {
        get => _discordRank;
        set
        {
            if (SetProperty(ref _discordRank, value))
            {
                OnPropertyChanged(nameof(PreviewRoleLine));
            }
        }
    }

    public string DiscordDivision
    {
        get => _discordDivision;
        set
        {
            if (SetProperty(ref _discordDivision, value))
            {
                OnPropertyChanged(nameof(PreviewRoleLine));
            }
        }
    }

    public string DiscordAvatarUrl
    {
        get => _discordAvatarUrl;
        set
        {
            if (SetProperty(ref _discordAvatarUrl, value))
            {
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
                OnPropertyChanged(nameof(PreviewImageUrl));
            }
        }
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

    public string PreviewDisplayName => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Username;

    public string PreviewImageUrl => !string.IsNullOrWhiteSpace(ShowcaseImageUrl) ? ShowcaseImageUrl : DiscordAvatarUrl;

    public string PreviewRoleLine =>
        string.IsNullOrWhiteSpace(DiscordDivision) ? DiscordRank : $"{DiscordRank} • {DiscordDivision}";

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
                ? "Profile loaded. No medals assigned yet."
                : $"Profile loaded. {dto.Medals.Count} medal(s) on record.";
        }
        catch
        {
            StatusMessage = "Unable to load your profile.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = "Saving showcase profile...";

        try
        {
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.PutAsJsonAsync(
                "api/v1/profiles/me/showcase",
                new UpdateMyShowcaseRequestDto(
                    EmptyToNull(ShowcaseImageUrl),
                    EmptyToNull(ShowcaseEyebrow),
                    EmptyToNull(ShowcaseTagline),
                    EmptyToNull(ShowcaseBio)),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = response.StatusCode == HttpStatusCode.NotFound
                    ? "Configured API does not support showcase editing yet."
                    : "Unable to save your showcase profile.";
                return;
            }

            var dto = await response.Content.ReadFromJsonAsync<CurrentProfileDto>(cancellationToken: ct);
            if (dto is null)
            {
                StatusMessage = "Unable to save your showcase profile.";
                return;
            }

            Apply(dto);
            StatusMessage = "Showcase profile saved.";
        }
        catch
        {
            StatusMessage = "Unable to save your showcase profile.";
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
        DiscordAvatarUrl = dto.DiscordAvatarUrl ?? string.Empty;
        ShowcaseImageUrl = dto.ShowcaseImageUrl ?? string.Empty;
        ShowcaseEyebrow = dto.ShowcaseEyebrow ?? InferEyebrow(dto.DiscordDivision);
        ShowcaseTagline = dto.ShowcaseTagline ?? InferTagline(dto.DiscordRank, dto.DiscordDivision);
        ShowcaseBio = dto.ShowcaseBio ?? string.Empty;

        Medals.Clear();
        foreach (var medal in dto.Medals)
        {
            Medals.Add(medal);
        }

        OnPropertyChanged(nameof(PreviewDisplayName));
        OnPropertyChanged(nameof(PreviewImageUrl));
        OnPropertyChanged(nameof(PreviewRoleLine));
    }

    private static string InferEyebrow(string? division) =>
        string.IsNullOrWhiteSpace(division) ? "HOUSE WOLF" : $"{division.ToUpperInvariant()} CORE";

    private static string InferTagline(string? rank, string? division)
    {
        var resolvedRank = string.IsNullOrWhiteSpace(rank) ? "House Wolf" : rank;
        return string.IsNullOrWhiteSpace(division) ? resolvedRank : $"{resolvedRank} • {division}";
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
