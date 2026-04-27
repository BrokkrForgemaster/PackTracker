using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.DTOs.Wiki;
using PackTracker.Domain.Enums;
using PackTracker.Presentation.Services;
using PackTracker.Presentation.Views;

namespace PackTracker.Presentation.ViewModels;

public class BlueprintComponentModifierPreview
{
    private static readonly TextInfo TextInfo =
        CultureInfo.CurrentCulture.TextInfo;

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

public sealed class OwnedBlueprintCardViewModel
{
    public Guid BlueprintId { get; init; }
    public Guid WikiUuid { get; init; }
    public string BlueprintName { get; init; } = string.Empty;
    public string CraftedItemName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string AvailabilityStatus { get; init; } = string.Empty;
    public string OwnershipStatus { get; init; } = string.Empty;
    public DateTime? VerifiedAt { get; init; }
    public string? Notes { get; init; }
    public int MaterialCount { get; init; }
    public string MaterialPreview { get; init; } = string.Empty;

    public static OwnedBlueprintCardViewModel FromDto(OwnedBlueprintSummaryDto dto)
    {
        var preview = dto.Materials.Count == 0
            ? "No material recipe saved yet."
            : string.Join(", ", dto.Materials.Take(3).Select(x => x.MaterialName));

        if (dto.Materials.Count > 3)
            preview += $" +{dto.Materials.Count - 3} more";

        return new OwnedBlueprintCardViewModel
        {
            BlueprintId = dto.BlueprintId,
            WikiUuid = dto.WikiUuid,
            BlueprintName = dto.BlueprintName,
            CraftedItemName = dto.CraftedItemName,
            Category = dto.Category,
            AvailabilityStatus = dto.AvailabilityStatus,
            OwnershipStatus = dto.OwnershipStatus,
            VerifiedAt = dto.VerifiedAt,
            Notes = dto.Notes,
            MaterialCount = dto.Materials.Count,
            MaterialPreview = preview
        };
    }
}

public partial class BlueprintExplorerViewModel : ObservableObject
{
    #region Fields

    private readonly IApiClientProvider _apiClientProvider;
    private readonly WikiBlueprintService _wikiBlueprints;
    private readonly ILogger<BlueprintExplorerViewModel> _logger;
    private CancellationTokenSource? _searchDebounce;
    private List<OwnedBlueprintSummaryDto> _ownedBlueprints = new();

    private const string AllCategoriesLabel = "All Categories";

    #endregion

    #region Collections

    public ObservableCollection<BlueprintSearchItemDto> Results { get; } = new();
    public ObservableCollection<OwnedBlueprintCardViewModel> OwnedBlueprints { get; } = new();
    public ObservableCollection<MaterialSelectionViewModel> Materials { get; } = new();
    public ObservableCollection<ComponentViewModel> Components { get; } = new();
    public ObservableCollection<BlueprintComponentModifierPreview> CombinedModifiers { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    #endregion

    #region State

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private bool inGameOnly;
    [ObservableProperty] private bool isResultsDropDownOpen;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Loading blueprints...";
    [ObservableProperty] private BlueprintSearchItemDto? selectedBlueprint;
    [ObservableProperty] private OwnedBlueprintCardViewModel? selectedOwnedBlueprint;
    [ObservableProperty] private BlueprintDetailDto? selectedBlueprintDetail;
    [ObservableProperty] private bool _isSelectedBlueprintOwned;
    [ObservableProperty] private string? procurementMaxClaimsText;

    [ObservableProperty] private int baseRpm = 650;
    [ObservableProperty] private int finalRpm;

    public bool HasSelectedBlueprintDetail => SelectedBlueprintDetail is not null;
    public bool HasOwnedBlueprints => OwnedBlueprints.Count > 0;
    public bool HasOwners => SelectedBlueprintDetail?.Owners?.Count > 0;
    public bool HasRewardPools => SelectedBlueprintDetail?.RewardPools?.GetType().GetProperty("pools") != null;
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
        await LoadOwnedBlueprintsAsync();
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

            IsResultsDropDownOpen = Results.Count > 0 && !string.IsNullOrWhiteSpace(SearchText);
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

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(SearchAsync);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        _ = SearchAsync();
    }

    partial void OnInGameOnlyChanged(bool value)
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
        RefreshOwnedFlag();
    }

    partial void OnSelectedOwnedBlueprintChanged(OwnedBlueprintCardViewModel? value)
    {
        if (value is not null)
            _ = SelectOwnedBlueprintAsync(value);
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
        OnPropertyChanged(nameof(HasOwnedBlueprints));
        RefreshOwnedFlag();
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
    private async Task SelectOwnedBlueprintAsync(OwnedBlueprintCardViewModel? blueprint)
    {
        if (blueprint is null)
            return;

        SelectedBlueprint = Results.FirstOrDefault(x => x.Id == blueprint.WikiUuid || x.Id == blueprint.BlueprintId);
        await LoadBlueprintDetailAsync(blueprint.WikiUuid != Guid.Empty ? blueprint.WikiUuid : blueprint.BlueprintId);
    }

    [RelayCommand]
    private async Task ExportOwnedBlueprintsAsync()
    {
        if (_ownedBlueprints.Count == 0)
        {
            StatusMessage = "No owned blueprints to export.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Owned Blueprints",
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"owned-blueprints-{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("Blueprint Name,Crafted Item,Category,Availability,Ownership Status,Verified At,Notes,Material Name,Quantity Required,Unit,Source Type");

            foreach (var blueprint in _ownedBlueprints.OrderBy(x => x.BlueprintName))
            {
                if (blueprint.Materials.Count == 0)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(blueprint.BlueprintName),
                        Csv(blueprint.CraftedItemName),
                        Csv(blueprint.Category),
                        Csv(blueprint.AvailabilityStatus),
                        Csv(blueprint.OwnershipStatus),
                        Csv(blueprint.VerifiedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? string.Empty),
                        Csv(blueprint.Notes ?? string.Empty),
                        Csv(string.Empty),
                        Csv(string.Empty),
                        Csv(string.Empty),
                        Csv(string.Empty)));

                    continue;
                }

                foreach (var material in blueprint.Materials.OrderBy(x => x.MaterialName))
                {
                    builder.AppendLine(string.Join(",",
                        Csv(blueprint.BlueprintName),
                        Csv(blueprint.CraftedItemName),
                        Csv(blueprint.Category),
                        Csv(blueprint.AvailabilityStatus),
                        Csv(blueprint.OwnershipStatus),
                        Csv(blueprint.VerifiedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? string.Empty),
                        Csv(blueprint.Notes ?? string.Empty),
                        Csv(material.MaterialName),
                        Csv(material.QuantityRequired.ToString("0.##", CultureInfo.InvariantCulture)),
                        Csv(material.Unit),
                        Csv(material.SourceType)));
                }
            }

            await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), Encoding.UTF8);
            StatusMessage = $"Exported {_ownedBlueprints.Count} owned blueprints to {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Owned blueprint export failed.");
            StatusMessage = $"Export failed: {ex.Message}";
        }
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

        var blueprintId = GetOperationBlueprintId();
        var formVm = new CraftingRequestFormViewModel(
            blueprintId,
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
                BlueprintId = blueprintId,
                CraftedItemName = SelectedBlueprintDetail.CraftedItemName,
                QuantityRequested = formVm.QuantityRequested,
                MinimumQuality = formVm.MinimumQuality,
                MaxClaims = formVm.MaxClaims,
                Priority = formVm.Priority,
                MaterialSupplyMode = formVm.MaterialSupplyMode,
                RewardOffered = formVm.RewardOffered,
                DeliveryLocation = formVm.DeliveryLocation,
                Notes = formVm.Notes,
                RequesterTimeZoneDisplayName = formVm.RequesterTimeZoneDisplayName,
                RequesterUtcOffsetMinutes = formVm.RequesterUtcOffsetMinutes
            };

            var response = await client.PostAsJsonAsync("api/v1/crafting/requests", dto);

            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<CreateCraftingRequestResponse>();
                StatusMessage = $"Crafting request submitted for {SelectedBlueprintDetail.BlueprintName}.";

                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                {
                    await mainWindow.NavigateToCraftingQueueAsync(payload?.RequestId);
                }
            }
            else
            {
                StatusMessage = $"Error {(int)response.StatusCode}: {TrimForDisplay(await response.Content.ReadAsStringAsync())}";
            }
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

            int? maxClaims = null;
            if (!string.IsNullOrWhiteSpace(ProcurementMaxClaimsText))
            {
                if (!int.TryParse(ProcurementMaxClaimsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxClaims)
                    || parsedMaxClaims < 1)
                {
                    StatusMessage = "Procurement max claims must be a whole number greater than 0.";
                    return;
                }

                maxClaims = parsedMaxClaims;
            }

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
                    MaxClaims = maxClaims,
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

    [RelayCommand]
    private async Task SyncWikiAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Syncing blueprints from Star Citizen wiki — this may take a few minutes...";

            using var client = _apiClientProvider.CreateClient();
            var response = await client.PostAsync("api/v1/wiki/sync/blueprints", null);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<WikiSyncResult>();
                StatusMessage = result is not null
                    ? $"Wiki sync complete: {result.Created} created, {result.Updated} updated, {result.Failed} failed."
                    : "Wiki sync complete.";

                await LoadWikiCacheAsync();
                await SearchAsync();
            }
            else
            {
                StatusMessage = $"Wiki sync failed: {TrimForDisplay(await response.Content.ReadAsStringAsync())}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wiki sync failed.");
            StatusMessage = $"Wiki sync error: {ex.Message}";
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

            var blueprintId = GetOperationBlueprintId();

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

            // Refresh the detail from the wiki service to get updated owner lists
            var updatedDetail = await _wikiBlueprints.GetDetailAsync(GetLookupBlueprintId());
            SelectedBlueprintDetail = updatedDetail;

            await LoadOwnedBlueprintsAsync();

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

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty("message", out var messageElement)
                    && messageElement.ValueKind == JsonValueKind.String)
                    return messageElement.GetString() ?? "<empty response>";

                if (document.RootElement.TryGetProperty("error", out var errorElement)
                    && errorElement.ValueKind == JsonValueKind.String)
                    return errorElement.GetString() ?? "<empty response>";
            }
        }
        catch (JsonException)
        {
            // Fall back to compact text rendering for non-JSON payloads.
        }

        var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 300 ? compact : compact[..300] + "...";
    }

    private Guid GetOperationBlueprintId()
    {
        if (SelectedBlueprintDetail is null)
            return Guid.Empty;

        return SelectedBlueprintDetail.Id != Guid.Empty
            ? SelectedBlueprintDetail.Id
            : SelectedBlueprintDetail.WikiUuid;
    }

    private Guid GetLookupBlueprintId()
    {
        if (SelectedBlueprintDetail is null)
            return Guid.Empty;

        return SelectedBlueprintDetail.WikiUuid != Guid.Empty
            ? SelectedBlueprintDetail.WikiUuid
            : SelectedBlueprintDetail.Id;
    }

    private async Task LoadOwnedBlueprintsAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            var items = await client.GetFromJsonAsync<List<OwnedBlueprintSummaryDto>>("api/v1/blueprints/owned")
                ?? new List<OwnedBlueprintSummaryDto>();

            _ownedBlueprints = items;

            OwnedBlueprints.Clear();
            foreach (var item in items)
                OwnedBlueprints.Add(OwnedBlueprintCardViewModel.FromDto(item));

            OnPropertyChanged(nameof(HasOwnedBlueprints));
            RefreshOwnedFlag();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Owned blueprint list failed to load.");
        }
    }

    private void RefreshOwnedFlag()
    {
        if (SelectedBlueprintDetail is null)
        {
            IsSelectedBlueprintOwned = false;
            return;
        }

        var selectedId = SelectedBlueprintDetail.Id;
        var selectedWikiUuid = SelectedBlueprintDetail.WikiUuid;

        IsSelectedBlueprintOwned = _ownedBlueprints.Any(x =>
            x.BlueprintId == selectedId ||
            (selectedWikiUuid != Guid.Empty && x.WikiUuid == selectedWikiUuid));
    }

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    private sealed class CreateCraftingRequestResponse
    {
        public Guid RequestId { get; set; }
    }

    #endregion
}
