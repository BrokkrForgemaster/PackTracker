using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private AdminMedalDefinitionDto? _selectedRibbonDefinition;

    public ObservableCollection<AdminMedalDefinitionDto> Medals { get; } = new();
    public ObservableCollection<AdminMedalAwardDto> Awards { get; } = new();
    public ObservableCollection<AdminMedalDefinitionDto> RibbonDefinitions { get; } = new();

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

    public AdminMedalDefinitionDto? SelectedRibbonDefinition
    {
        get => _selectedRibbonDefinition;
        set => SetProperty(ref _selectedRibbonDefinition, value);
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
        RibbonDefinitions.Clear();

        foreach (var award in dto.AvailableMedals)
        {
            if (string.Equals(award.AwardType, "Ribbon", StringComparison.OrdinalIgnoreCase))
                RibbonDefinitions.Add(award);
            else
                Medals.Add(award);
        }

        foreach (var award in dto.Awards)
            Awards.Add(award);

        StatusMessage = $"Loaded {Medals.Count} medals, {RibbonDefinitions.Count} ribbons, and {Awards.Count} awards.";
    }

    public Task<AwardRibbonResultDto?> AwardRibbonAsync(AwardRibbonRequestDto request)
    {
        return _api.AwardRibbonAsync(request);
    }

    public void PrepareRibbonsForImport()
    {
        StatusMessage = "Use Load Award File to load ribbons.json, then click Import Awards.";
    }

    public async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportJson))
        {
            StatusMessage = "Load or paste medal/ribbon JSON first.";
            return;
        }

        var request = JsonSerializer.Deserialize<ImportMedalsRequestDto>(
            ImportJson,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (request is null)
        {
            StatusMessage = "Unable to parse award JSON.";
            return;
        }

        var result = await _api.ImportMedalsAsync(request);
        await LoadAsync();

        var unmatched = result.UnmatchedRecipients.Count;
        var unknown = result.UnknownMedals.Count;

        StatusMessage =
            $"Imported: +{result.MedalDefinitionsCreated} new definitions, {result.MedalDefinitionsUpdated} updated, " +
            $"+{result.AwardsCreated} awards, {result.AwardsSkipped} skipped, {unmatched} unmatched recipients, {unknown} unknown definitions.";
    }
}