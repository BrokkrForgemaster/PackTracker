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
    private RibbonEntry? _selectedRibbon;

    public ObservableCollection<AdminMedalDefinitionDto> Medals { get; } = new();
    public ObservableCollection<AdminMedalAwardDto> Awards { get; } = new();
    public ObservableCollection<RibbonEntry> Ribbons { get; } = new();

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

    public RibbonEntry? SelectedRibbon
    {
        get => _selectedRibbon;
        set => SetProperty(ref _selectedRibbon, value);
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
            Medals.Add(medal);

        foreach (var award in dto.Awards)
            Awards.Add(award);

        await LoadRibbonsAsync();

        StatusMessage = $"Loaded {Medals.Count} medals, {Awards.Count} awards, and {Ribbons.Count} ribbon definitions.";
    }

    public async Task<AwardRibbonResultDto?> AwardRibbonAsync(AwardRibbonRequestDto request)
        => await _api.AwardRibbonAsync(request);

    public async Task LoadRibbonsAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "data", "ribbons.json");
        if (!File.Exists(path)) return;

        var json = await File.ReadAllTextAsync(path);
        var doc = JsonSerializer.Deserialize<RibbonsFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (doc is null) return;

        Ribbons.Clear();
        foreach (var r in doc.AvailableRibbons)
            Ribbons.Add(new RibbonEntry(r.Name, r.Description, r.Image));
    }

    public void PrepareRibbonsForImport()
    {
        if (Ribbons.Count == 0)
        {
            StatusMessage = "No ribbon definitions loaded.";
            return;
        }

        var request = new ImportMedalsRequestDto(
            Ribbons.Select(r => new ImportMedalDefinitionDto(r.Name, r.Description, r.RawImagePath)).ToList(),
            new Dictionary<string, IReadOnlyList<string>>());

        ImportJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
        StatusMessage = $"Prepared {Ribbons.Count} ribbon definitions for import. Review and click 'Import Medals' to proceed.";
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

    private sealed record RibbonsFile(
        [property: JsonPropertyName("available_ribbons")] IReadOnlyList<RibbonJson> AvailableRibbons);

    private sealed record RibbonJson(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("image")] string Image);
}
