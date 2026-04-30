using System.Collections.ObjectModel;
using System.Text.Json;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.Services.Admin;

namespace PackTracker.Presentation.ViewModels.Admin;

public sealed class AdminMedalsViewModel : ViewModelBase
{
    private readonly AdminApiClient _api;
    private string _importJson = string.Empty;
    private string _statusMessage = string.Empty;
    private AdminMedalDefinitionDto? _selectedMedal;
    private AdminMedalAwardDto? _selectedAward;

    public ObservableCollection<AdminMedalDefinitionDto> Medals { get; } = new();
    public ObservableCollection<AdminMedalAwardDto> Awards { get; } = new();

    public string ImportJson
    {
        get => _importJson;
        set => SetProperty(ref _importJson, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AdminMedalDefinitionDto? SelectedMedal
    {
        get => _selectedMedal;
        set => SetProperty(ref _selectedMedal, value);
    }

    public AdminMedalAwardDto? SelectedAward
    {
        get => _selectedAward;
        set => SetProperty(ref _selectedAward, value);
    }

    public AdminMedalsViewModel(AdminApiClient api)
    {
        _api = api;
    }

    public async Task LoadAsync()
    {
        var dto = await _api.GetMedalsAsync();

        Medals.Clear();
        Awards.Clear();

        foreach (var medal in dto.AvailableMedals)
        {
            Medals.Add(medal);
        }

        foreach (var award in dto.Awards)
        {
            Awards.Add(award);
        }

        StatusMessage = $"Loaded {Medals.Count} medals and {Awards.Count} awards.";
    }

    public async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportJson))
        {
            StatusMessage = "Paste medal JSON first.";
            return;
        }

        var request = JsonSerializer.Deserialize<ImportMedalsRequestDto>(ImportJson);
        if (request is null)
        {
            StatusMessage = "Unable to parse medal JSON.";
            return;
        }

        var result = await _api.ImportMedalsAsync(request);
        await LoadAsync();

        var unmatched = result.UnmatchedRecipients.Count;
        var unknown = result.UnknownMedals.Count;
        StatusMessage =
            $"Imported: +{result.MedalDefinitionsCreated} new medals, {result.MedalDefinitionsUpdated} updated, " +
            $"+{result.AwardsCreated} awards, {result.AwardsSkipped} skipped, {unmatched} unmatched recipients, {unknown} unknown medals.";
    }
}
