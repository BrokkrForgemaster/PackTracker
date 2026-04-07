using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Enums;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public class BlueprintComponentModifierPreview
{
    public string PropertyKey { get; set; } = string.Empty;
    public double CalculatedValue { get; set; }

    public string DisplayName => PropertyKey
        .Replace("weapon_", "")
        .Replace("_", " ")
        .Trim();
}

public partial class BlueprintExplorerViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly WikiBlueprintService _wikiBlueprints;
    private readonly ILogger<BlueprintExplorerViewModel> _logger;

    public ObservableCollection<BlueprintSearchItemDto> Results { get; } = new();
    public ObservableCollection<BlueprintRecipeMaterialDto> Materials { get; } = new();
    public ObservableCollection<ComponentViewModel> Components { get; } = new();
    public ObservableCollection<BlueprintComponentModifierPreview> CombinedModifiers { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private bool inGameOnly = false;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Loading blueprints...";
    [ObservableProperty] private BlueprintSearchItemDto? selectedBlueprint;
    [ObservableProperty] private BlueprintDetailDto? selectedBlueprintDetail;
    [ObservableProperty] private BlueprintRecipeMaterialDto? selectedMaterial;

    // Final stats
    [ObservableProperty] private int baseRpm = 650;
    [ObservableProperty] private int finalRpm;

    public bool HasSelectedBlueprintDetail => SelectedBlueprintDetail is not null;

    private const string AllCategoriesLabel = "All Categories";

    public IReadOnlyList<MemberBlueprintInterestType> InterestTypeOptions { get; } =
        Enum.GetValues<MemberBlueprintInterestType>();

    public BlueprintExplorerViewModel(
        IApiClientProvider apiClientProvider,
        WikiBlueprintService wikiBlueprints,
        ILogger<BlueprintExplorerViewModel> logger)
    {
        _apiClientProvider = apiClientProvider;
        _wikiBlueprints = wikiBlueprints;
        _logger = logger;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        LoadCategoriesFromWiki();
        await LoadWikiCacheAsync();
        await SearchAsync();
    }

    private void LoadCategoriesFromWiki()
    {
        Categories.Clear();
        Categories.Add(AllCategoriesLabel);
        foreach (var cat in WikiBlueprintService.KnownCategories)
            Categories.Add(cat);

        SelectedCategory = AllCategoriesLabel;
    }

    private async Task LoadWikiCacheAsync()
    {
        try
        {
            StatusMessage = "Loading blueprints from Star Citizen wiki...";
            var count = await _wikiBlueprints.LoadAllAsync();
            StatusMessage = count > 0
                ? $"Loaded {count} blueprints. Use search to filter."
                : "Could not reach Star Citizen wiki API.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki cache load failed.");
            StatusMessage = $"Wiki load error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            IsLoading = true;

            if (!_wikiBlueprints.IsLoaded)
            {
                StatusMessage = "Still loading blueprint data from wiki...";
                await LoadWikiCacheAsync();
            }

            var effectiveCategory = SelectedCategory == AllCategoriesLabel ? null : SelectedCategory;
            var items = _wikiBlueprints.Search(SearchText, effectiveCategory);

            Results.Clear();
            foreach (var item in items)
                Results.Add(item);

            StatusMessage = Results.Count == 0
                ? "No blueprints matched your search."
                : $"{Results.Count} results.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint search failed.");
            StatusMessage = $"Search error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedBlueprintChanged(BlueprintSearchItemDto? value)
    {
        if (value is not null)
            _ = LoadBlueprintDetailAsync(value.Id);
    }

    partial void OnSelectedBlueprintDetailChanged(BlueprintDetailDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedBlueprintDetail));
    }

    private async Task LoadBlueprintDetailAsync(Guid blueprintId)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading blueprint components and modifiers...";

            var detail = await _wikiBlueprints.GetDetailAsync(blueprintId);

            SelectedBlueprintDetail = detail;
            Materials.Clear();
            Components.Clear();
            CombinedModifiers.Clear();

            if (detail is null)
            {
                StatusMessage = "Blueprint detail not found.";
                return;
            }

            // Load recipe materials
            foreach (var material in detail.Materials)
                Materials.Add(material);

            // Load components + converter stats (modifiers) from API
            if (detail.Components != null && detail.Components.Any())
            {
                foreach (var component in detail.Components)
                {
                    var vm = new ComponentViewModel
                    {
                        Parent = this,
                        PartName = component.PartName ?? "Unknown Part",
                        MaterialName = component.MaterialName ?? "Unknown Material",
                        Scu = component.Scu,
                        Quantity = (int)(component.Quantity > 0 ? component.Quantity : 1),
                        QualityValue = component.DefaultQuality
                    };

                    // Add modifiers from API
                    if (component.Modifiers != null)
                    {
                        foreach (var mod in component.Modifiers)
                        {
                            double baseValue = mod.ValueAt1000 > 0
                                ? mod.ValueAt1000
                                : (mod.AtMinQuality + mod.AtMaxQuality) / 2.0;

                            vm.Modifiers.Add(new StatModifier(mod.PropertyKey, baseValue, vm));
                        }
                    }

                    Components.Add(vm);
                }
            }

            UpdateCombinedModifiers();

            StatusMessage = $"Loaded {detail.BlueprintName} with {Components.Count} components.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint detail failed for {BlueprintId}", blueprintId);
            StatusMessage = $"Error loading blueprint: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void UpdateCombinedModifiers()
    {
        CombinedModifiers.Clear();

        var grouped = Components
            .SelectMany<ComponentViewModel, StatModifier>(c => c.Modifiers)
            .GroupBy<StatModifier, string>(m => m.StatName);

        foreach (var group in grouped)
        {
            double total = group.Sum<StatModifier>(m => m.BaseValue * (m.ParentComponent.QualityValue / 1000.0));

            CombinedModifiers.Add(new BlueprintComponentModifierPreview
            {
                PropertyKey = group.Key,
                CalculatedValue = total
            });
        }

        // Calculate Final RPM based on fire rate modifier
        var fireRateMod = CombinedModifiers.FirstOrDefault(m =>
            m.PropertyKey.Contains("fire_rate", StringComparison.OrdinalIgnoreCase))?.CalculatedValue ?? 0;

        FinalRpm = (int)Math.Round(BaseRpm * (1 + fireRateMod / 100.0));
    }

    // ─────────────────────────────────────────────────────────────
    // Action commands
    // ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task MarkOwnedAsync()
    {
        await PostOwnershipAsync(MemberBlueprintInterestType.Owns, "Marked as Owned.");
    }

    [RelayCommand]
    private async Task MarkWantedAsync()
    {
        await PostOwnershipAsync(MemberBlueprintInterestType.Wants, "Marked as Wanted.");
    }

    [RelayCommand]
    private async Task CreateCraftingRequestAsync()
    {
        if (SelectedBlueprintDetail is null) return;
        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            var dto = new CreateCraftingRequestDto
            {
                BlueprintId = SelectedBlueprintDetail.Id,
                QuantityRequested = 1,
                Priority = RequestPriority.Normal
            };
            var response = await client.PostAsJsonAsync("api/v1/crafting/requests", dto);
            StatusMessage = response.IsSuccessStatusCode
                ? $"Crafting request created for {SelectedBlueprintDetail.BlueprintName}."
                : $"Error {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateCraftingRequest failed.");
            StatusMessage = $"Failed to create crafting request: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CreateProcurementRequestAsync()
    {
        if (SelectedMaterial is null) return;
        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            var dto = new CreateMaterialProcurementRequestDto
            {
                MaterialId = SelectedMaterial.MaterialId,
                QuantityRequested = (decimal)SelectedMaterial.QuantityRequired,
                Priority = RequestPriority.Normal
            };
            var response = await client.PostAsJsonAsync("api/v1/crafting/procurement-requests", dto);
            StatusMessage = response.IsSuccessStatusCode
                ? $"Procurement request created for {SelectedMaterial.MaterialName}."
                : $"Error {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateProcurementRequest failed.");
            StatusMessage = $"Failed to create procurement request: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private async Task PostOwnershipAsync(MemberBlueprintInterestType interestType, string successMessage)
    {
        if (SelectedBlueprintDetail is null) return;
        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            var dto = new RegisterBlueprintOwnershipRequest
            {
                InterestType = interestType,
                AvailabilityStatus = "Available"
            };
            var response = await client.PostAsJsonAsync(
                $"api/v1/blueprints/{SelectedBlueprintDetail.Id}/ownership", dto);
            StatusMessage = response.IsSuccessStatusCode
                ? successMessage
                : $"Error {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostOwnership failed.");
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    // ─────────────────────────────────────────────────────────────
    // Helper methods
    // ─────────────────────────────────────────────────────────────

    private static string TrimForDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "<empty response>";
        var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 300 ? compact : compact[..300] + "...";
    }
}