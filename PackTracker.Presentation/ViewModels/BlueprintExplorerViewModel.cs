using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.ViewModels;

public class StatModifierPreview
{
    public string PropertyKey { get; set; } = string.Empty;
    public double CalculatedValue { get; set; }
}

public partial class BlueprintExplorerViewModel : ObservableObject
{
    private readonly ILogger<BlueprintExplorerViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private List<JsonElement> _cachedRawData = new();

    public ObservableCollection<BlueprintSearchItemDto> Results { get; } = new();
    public ObservableCollection<BlueprintRecipeMaterialDto> Materials { get; } = new();
    public ObservableCollection<ComponentViewModel> SelectedComponents { get; } = new();
    public ObservableCollection<StatModifierPreview> QualityModifiers { get; } = new();

    [ObservableProperty] private BlueprintSearchItemDto? selectedBlueprint;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Ready";

    public BlueprintExplorerViewModel(ILogger<BlueprintExplorerViewModel> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _httpClient = new HttpClient();
        _ = SearchAsync();
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Syncing with Star Citizen Wiki (GitHub)...";

            var url = _settingsService.GetSettings().BlueprintDataSourceUrl;
            if (string.IsNullOrEmpty(url)) return;

            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonContent);

            // Clone elements so they persist after the document is disposed
            _cachedRawData = doc.RootElement.EnumerateArray().Select(x => x.Clone()).ToList();

            ApplyLocalFilters();
            StatusMessage = $"Sync Complete. {_cachedRawData.Count} items found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub Sync Failed.");
            StatusMessage = $"Sync Failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private void ApplyLocalFilters()
    {
        Results.Clear();
        var filtered = _cachedRawData.Where(x => 
            string.IsNullOrEmpty(SearchText) || 
            x.GetProperty("output").GetProperty("name").GetString()!
             .Contains(SearchText, StringComparison.OrdinalIgnoreCase)).Take(50);

        foreach (var item in filtered)
        {
            Results.Add(new BlueprintSearchItemDto {
                Id = item.GetProperty("uuid").GetGuid(),
                BlueprintName = item.GetProperty("output").GetProperty("name").GetString() ?? "Unknown",
                Category = item.TryGetProperty("kind", out var k) ? k.GetString() ?? "Item" : "Item"
            });
        }
    }

    partial void OnSelectedBlueprintChanged(BlueprintSearchItemDto? value)
    {
        if (value == null) return;
        LoadBlueprintDetails(value.Id);
    }

    private void LoadBlueprintDetails(Guid blueprintId)
    {
        SelectedComponents.Clear();
        Materials.Clear();

        var rawItem = _cachedRawData.FirstOrDefault(x => x.GetProperty("uuid").GetGuid() == blueprintId);

        if (rawItem.ValueKind != JsonValueKind.Undefined && rawItem.TryGetProperty("recipe", out var recipe))
        {
            int partIndex = 1;
            foreach (var matElement in recipe.EnumerateArray())
            {
                var matName = matElement.GetProperty("name").GetString() ?? "Unknown";
                var qty = matElement.GetProperty("quantity").GetDouble();

                // 1. Add to the Mining Materials list (The "Recipe")
                Materials.Add(new BlueprintRecipeMaterialDto { Name = matName, Quantity = qty });

                // 2. Add to the Simulator list (The "Parts")
                var component = new ComponentViewModel
                {
                    Parent = this,
                    PartName = GetPartName(partIndex++),
                    MaterialName = matName,
                    Quantity = qty,
                    QualityValue = 500
                };
                AddSampleModifiers(component);
                SelectedComponents.Add(component);
            }
        }
        UpdateTotalModifiers();
    }

    private string GetPartName(int index) => index switch { 1 => "Frame", 2 => "Stock", 3 => "Barrel", _ => $"Component {index}" };

    public void UpdateTotalModifiers()
    {
        QualityModifiers.Clear();
        var allStats = SelectedComponents.SelectMany(c => c.Modifiers).GroupBy(m => m.StatName);

        foreach (var group in allStats)
        {
            double totalImpact = group.Sum(m => m.Percentage * (m.ParentComponent.QualityValue / 1000.0));
            QualityModifiers.Add(new StatModifierPreview { PropertyKey = group.Key, CalculatedValue = totalImpact });
        }
    }

    private void AddSampleModifiers(ComponentViewModel comp)
    {
        if (comp.PartName == "Frame") comp.Modifiers.Add(new StatModifier("Handling", 0.12, comp));
        if (comp.PartName == "Barrel") comp.Modifiers.Add(new StatModifier("Damage", -0.05, comp));
    }

    partial void OnSearchTextChanged(string value) => ApplyLocalFilters();
}