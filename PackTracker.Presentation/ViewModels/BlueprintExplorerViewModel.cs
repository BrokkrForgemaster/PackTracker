using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Enums;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public class BlueprintComponentModifierPreview
{
    private static readonly System.Globalization.TextInfo TextInfo =
        System.Globalization.CultureInfo.CurrentCulture.TextInfo;

    public string PropertyKey { get; set; } = string.Empty;
    public double CalculatedValue { get; set; }

    public string DisplayName => TextInfo.ToTitleCase(
        PropertyKey
            .Replace("weapon_", "")
            .Replace("firerate", "fire rate")
            .Replace("_", " ")
            .Trim());

    public bool IsInverseBenefitStat =>
        PropertyKey.Contains("recoil", StringComparison.OrdinalIgnoreCase) ||
        PropertyKey.Contains("kick", StringComparison.OrdinalIgnoreCase) ||
        PropertyKey.Contains("spread", StringComparison.OrdinalIgnoreCase) ||
        PropertyKey.Contains("sway", StringComparison.OrdinalIgnoreCase) ||
        PropertyKey.Contains("bloom", StringComparison.OrdinalIgnoreCase);

    public double DisplayValue => IsInverseBenefitStat ? -CalculatedValue : CalculatedValue;

    public bool IsPositive => DisplayValue > 0;
    public bool IsNegative => DisplayValue < 0;
}

public partial record QualityTier(string Label, int MinValue);

public partial class MaterialSelectionViewModel : ObservableObject
{
    private readonly BlueprintRecipeMaterialDto _data;

    public Guid MaterialId => _data.MaterialId;
    public string MaterialName => _data.MaterialName;
    public double QuantityRequired => _data.QuantityRequired;
    public string Unit => _data.Unit;
    public string SourceType => _data.SourceType;

    public static IReadOnlyList<QualityTier> QualityTiers { get; } = new[]
    {
        new QualityTier("500 - 699", 500),
        new QualityTier("700 - 750", 700),
        new QualityTier("751 - 799", 751),
        new QualityTier("800 - 850", 800),
        new QualityTier("851 - 900", 851),
        new QualityTier("901 - 950", 901),
        new QualityTier("951 - 999", 951),
    };

    [ObservableProperty] private bool isSelected = false;
    [ObservableProperty] private QualityTier selectedQualityTier = QualityTiers[0];
    [ObservableProperty] private string rewardOffered = "Negotiable";

    public int RequestedQuality => SelectedQualityTier.MinValue;

    public MaterialSelectionViewModel(BlueprintRecipeMaterialDto data)
    {
        _data = data;
    }
}

public partial class BlueprintExplorerViewModel : ObservableObject
{
    #region Fields

    private readonly IApiClientProvider _apiClientProvider;
    private readonly WikiBlueprintService _wikiBlueprints;
    private readonly ILogger<BlueprintExplorerViewModel> _logger;

    private const string AllCategoriesLabel = "All Categories";

    #endregion

    #region Collections

    public ObservableCollection<BlueprintSearchItemDto> Results { get; } = new();
    public ObservableCollection<MaterialSelectionViewModel> Materials { get; } = new();
    public ObservableCollection<ComponentViewModel> Components { get; } = new();
    public ObservableCollection<BlueprintComponentModifierPreview> CombinedModifiers { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    #endregion

    #region State

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private bool inGameOnly;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Loading blueprints...";
    [ObservableProperty] private BlueprintSearchItemDto? selectedBlueprint;
    [ObservableProperty] private BlueprintDetailDto? selectedBlueprintDetail;

    [ObservableProperty] private int baseRpm = 650;
    [ObservableProperty] private int finalRpm;

    public bool HasSelectedBlueprintDetail => SelectedBlueprintDetail is not null;
    public bool HasRewardPools => SelectedBlueprintDetail?.RewardPools.GetType().GetProperty("pools") != null;
    public bool HasComponents => Components.Count > 0;
    public bool HasMaterials => Materials.Count > 0;
    public bool HasCombinedModifiers => CombinedModifiers.Count > 0;

    public IReadOnlyList<MemberBlueprintInterestType> InterestTypeOptions { get; } =
        Enum.GetValues<MemberBlueprintInterestType>();

    #endregion

    #region Constructor

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

    #endregion

    #region Initialization

    private async Task InitializeAsync()
    {
        LoadCategoriesFromWiki();
        await LoadWikiCacheAsync();
        await SearchAsync();
    }

    private void LoadCategoriesFromWiki()
    {
        var selected = SelectedCategory;

        Categories.Clear();
        Categories.Add(AllCategoriesLabel);

        var dynamicCategories = _wikiBlueprints.GetCategories();
        foreach (var cat in dynamicCategories)
        {
            if (!string.IsNullOrWhiteSpace(cat))
                Categories.Add(cat);
        }

        if (selected != null && Categories.Contains(selected))
            SelectedCategory = selected;
        else
            SelectedCategory = AllCategoriesLabel;
    }

    private async Task LoadWikiCacheAsync()
    {
        try
        {
            StatusMessage = "Loading blueprints from Star Citizen wiki...";
            var count = await _wikiBlueprints.LoadAllAsync();

            LoadCategoriesFromWiki();

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

    #endregion

    #region Search / Selection

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
            var items = _wikiBlueprints.Search(SearchText, effectiveCategory, InGameOnly);

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

    partial void OnSelectedCategoryChanged(string? value)
    {
        _ = SearchAsync();
    }

    partial void OnSelectedBlueprintChanged(BlueprintSearchItemDto? value)
    {
        if (value is not null)
            _ = LoadBlueprintDetailAsync(value.Id);
    }

    partial void OnSelectedBlueprintDetailChanged(BlueprintDetailDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedBlueprintDetail));
        OnPropertyChanged(nameof(HasRewardPools));
    }

    private async Task LoadBlueprintDetailAsync(Guid blueprintId)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading blueprint components and modifiers...";

            var detail = await _wikiBlueprints.GetDetailAsync(blueprintId);

            System.Diagnostics.Debug.WriteLine(
                $"Blueprint detail refreshed. Id={blueprintId}, OwnerCount={detail?.OwnerCount}");

            SelectedBlueprintDetail = detail;

            Materials.Clear();
            Components.Clear();
            CombinedModifiers.Clear();

            if (detail is null)
            {
                StatusMessage = "Blueprint detail not found.";
                RaiseDetailFlags();
                return;
            }

            foreach (var material in detail.Materials)
                Materials.Add(new MaterialSelectionViewModel(material));

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

                    if (component.Modifiers != null)
                    {
                        foreach (var mod in component.Modifiers)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"Part={component.PartName}, Material={component.MaterialName}, Stat={mod.PropertyKey}, AtMin={mod.AtMinQuality}, AtMax={mod.AtMaxQuality}");

                            vm.Modifiers.Add(new StatModifier(
                                mod.PropertyKey,
                                mod.AtMinQuality,
                                mod.AtMaxQuality,
                                vm));
                        }
                    }

                    Components.Add(vm);
                }
            }

            UpdateCombinedModifiers();
            RaiseDetailFlags();

            StatusMessage = $"Loaded {detail.BlueprintName} with {Components.Count} components.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint detail failed for {BlueprintId}", blueprintId);
            StatusMessage = $"Error loading blueprint: {ex.Message}";
            RaiseDetailFlags();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RaiseDetailFlags()
    {
        OnPropertyChanged(nameof(HasComponents));
        OnPropertyChanged(nameof(HasMaterials));
        OnPropertyChanged(nameof(HasCombinedModifiers));
        OnPropertyChanged(nameof(HasRewardPools));
    }

    #endregion

    #region Combined Modifiers

    public void UpdateCombinedModifiers()
    {
        CombinedModifiers.Clear();

        var grouped = Components
            .SelectMany(c => c.Modifiers)
            .GroupBy(m => m.StatName);

        foreach (var group in grouped)
        {
            double total = group.Sum(m => m.EffectiveAtQuality(m.ParentComponent.QualityValue));

            CombinedModifiers.Add(new BlueprintComponentModifierPreview
            {
                PropertyKey = group.Key,
                CalculatedValue = total
            });
        }

        var fireRateMod = CombinedModifiers.FirstOrDefault(m =>
            m.PropertyKey.Contains("fire_rate", StringComparison.OrdinalIgnoreCase) ||
            m.PropertyKey.Contains("firerate", StringComparison.OrdinalIgnoreCase))?.CalculatedValue ?? 0;

        FinalRpm = (int)Math.Round(BaseRpm * (1 + fireRateMod / 100.0));

        OnPropertyChanged(nameof(HasCombinedModifiers));
    }

    #endregion

    #region Actions

    [RelayCommand]
    private async Task MarkOwnedAsync()
    {
        await PostOwnershipAsync(MemberBlueprintInterestType.Owns, "Marked as Owned.");
    }

    [RelayCommand]
    private void SelectAllMaterials()
    {
        foreach (var m in Materials)
            m.IsSelected = true;
    }

    [RelayCommand]
    private void ClearAllMaterials()
    {
        foreach (var m in Materials)
            m.IsSelected = false;
    }

    [RelayCommand]
    private async Task CreateCraftingRequestAsync()
    {
        if (SelectedBlueprintDetail is null)
            return;

        var formVm = new CraftingRequestFormViewModel(
            SelectedBlueprintDetail.Id,
            SelectedBlueprintDetail.BlueprintName);

        var dialog = new PackTracker.Presentation.Views.CraftingRequestFormDialog(formVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            IsLoading = true;

            using var client = _apiClientProvider.CreateClient();
            var dto = new CreateCraftingRequestDto
            {
                BlueprintId = SelectedBlueprintDetail.Id,
                CraftedItemName = SelectedBlueprintDetail.CraftedItemName,
                QuantityRequested = formVm.QuantityRequested,
                MinimumQuality = formVm.MinimumQuality,
                Priority = formVm.Priority,
                MaterialSupplyMode = formVm.MaterialSupplyMode,
                RewardOffered = formVm.RewardOffered,
                DeliveryLocation = formVm.DeliveryLocation,
                Notes = formVm.Notes,
                RequesterTimeZoneDisplayName = formVm.RequesterTimeZoneDisplayName,
                RequesterUtcOffsetMinutes = formVm.RequesterUtcOffsetMinutes
            };

            var response = await client.PostAsJsonAsync("api/v1/crafting/requests", dto);

            StatusMessage = response.IsSuccessStatusCode
                ? $"Crafting request submitted for {SelectedBlueprintDetail.BlueprintName}."
                : $"Error {(int)response.StatusCode}: {TrimForDisplay(await response.Content.ReadAsStringAsync())}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateCraftingRequest failed.");
            StatusMessage = $"Failed to create crafting request: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateProcurementRequestAsync()
    {
        var selected = Materials.Where(m => m.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Select at least one material to request.";
            return;
        }

        try
        {
            IsLoading = true;

            using var client = _apiClientProvider.CreateClient();
            int success = 0;

            var errors = new List<string>();

            foreach (var material in selected)
            {
                var dto = new CreateMaterialProcurementRequestDto
                {
                    MaterialId = material.MaterialId,
                    MaterialName = material.MaterialName,
                    QuantityRequested = (decimal)material.QuantityRequired,
                    MinimumQuality = material.RequestedQuality,
                    Priority = RequestPriority.Normal,
                    PreferredForm = MaterialFormPreference.Any,
                    RewardOffered = string.IsNullOrWhiteSpace(material.RewardOffered)
                        ? "Negotiable"
                        : material.RewardOffered
                };

                var response = await client.PostAsJsonAsync("api/v1/crafting/procurement-requests", dto);
                if (response.IsSuccessStatusCode)
                    success++;
                else
                    errors.Add($"{material.MaterialName}: {(int)response.StatusCode}");
            }

            StatusMessage = success == selected.Count
                ? $"Created {success}/{selected.Count} procurement requests."
                : $"Created {success}/{selected.Count}. Failed: {string.Join(", ", errors)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateProcurementRequest failed.");
            StatusMessage = $"Failed to create procurement requests: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PostOwnershipAsync(MemberBlueprintInterestType interestType, string successMessage)
    {
        if (SelectedBlueprintDetail is null)
            return;

        try
        {
            IsLoading = true;

            var blueprintId = SelectedBlueprintDetail.Id;

            using var client = _apiClientProvider.CreateClient();
            var dto = new RegisterBlueprintOwnershipRequest
            {
                InterestType = interestType,
                AvailabilityStatus = "Available"
            };

            var response = await client.PostAsJsonAsync(
                $"api/v1/blueprints/{blueprintId}/ownership",
                dto);

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Error {(int)response.StatusCode}: {TrimForDisplay(await response.Content.ReadAsStringAsync())}";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Ownership post succeeded for blueprint {blueprintId}");

            await LoadBlueprintDetailAsync(blueprintId);

            System.Diagnostics.Debug.WriteLine(
                $"After refresh, OwnerCount={SelectedBlueprintDetail?.OwnerCount}");

            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostOwnership failed.");
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Helpers

    private static string TrimForDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty response>";

        var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 300 ? compact : compact[..300] + "...";
    }

    #endregion
}
