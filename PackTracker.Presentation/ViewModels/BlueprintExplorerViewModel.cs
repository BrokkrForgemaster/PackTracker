using System.Collections.ObjectModel;
using System.Net.Http;
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
}

public partial class BlueprintExplorerViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly WikiBlueprintService _wikiBlueprints;
    private readonly ILogger<BlueprintExplorerViewModel> _logger;

    public ObservableCollection<BlueprintSearchItemDto> Results { get; } = new();
    public ObservableCollection<BlueprintRecipeMaterialDto> Materials { get; } = new();
    public ObservableCollection<BlueprintOwnerDto> Owners { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<ComponentViewModel> Components { get; } = new();
    public ObservableCollection<BlueprintComponentModifierPreview> CombinedModifiers { get; } = new();

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private bool inGameOnly = false;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Loading blueprints...";
    [ObservableProperty] private BlueprintSearchItemDto? selectedBlueprint;
    [ObservableProperty] private BlueprintDetailDto? selectedBlueprintDetail;
    [ObservableProperty] private BlueprintRecipeMaterialDto? selectedMaterial;
    [ObservableProperty] private MemberBlueprintInterestType selectedInterestType = MemberBlueprintInterestType.Owns;

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
            StatusMessage = "Loading blueprint detail...";

            // Fetch blueprint data from wiki API
            var detail = await _wikiBlueprints.GetDetailAsync(blueprintId);

            SelectedBlueprintDetail = detail;
            Materials.Clear();
            Owners.Clear();
            Components.Clear();
            CombinedModifiers.Clear();
            SelectedMaterial = null;

            if (detail is null)
            {
                StatusMessage = "Blueprint detail not found.";
                return;
            }

            foreach (var material in detail.Materials)
                Materials.Add(material);

            foreach (var component in detail.Components)
            {
                var vm = new ComponentViewModel
                {
                    Parent = this,
                    PartName = component.PartName,
                    MaterialName = component.MaterialName,
                    Quantity = component.Quantity,
                    QualityValue = component.DefaultQuality
                };

                foreach (var modifier in component.Modifiers)
                {
                    var averaged = (modifier.AtMinQuality + modifier.AtMaxQuality) / 2d;
                    vm.Modifiers.Add(new StatModifier(modifier.PropertyKey, averaged, vm));
                }

                Components.Add(vm);
            }

            UpdateCombinedModifiers();

            // Load ownership records from local API (non-fatal if unavailable)
            await LoadOwnersFromLocalAsync(blueprintId);

            StatusMessage = $"Loaded {detail.BlueprintName}.";
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

    private async Task LoadOwnersFromLocalAsync(Guid blueprintId)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            if (client.BaseAddress is null) return;

            var response = await client.GetAsync($"api/v1/blueprints/{blueprintId}");
            if (!response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync();
            var local = JsonSerializer.Deserialize<BlueprintDetailDto>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (local?.Owners == null) return;

            Owners.Clear();
            foreach (var owner in local.Owners)
                Owners.Add(owner);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load owners from local API for {BlueprintId}", blueprintId);
        }
    }

    [RelayCommand]
    private async Task RegisterOwnershipAsync()
    {
        if (SelectedBlueprintDetail is null)
        {
            StatusMessage = "Select a blueprint before saving status.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Saving blueprint status...";

            using var client = _apiClientProvider.CreateClient();
            using var response = await client.PostAsJsonAsync(
                $"api/v1/blueprints/{SelectedBlueprintDetail.Id}/ownership",
                new RegisterBlueprintOwnershipRequest
                {
                    InterestType = SelectedInterestType,
                    AvailabilityStatus = SelectedInterestType == MemberBlueprintInterestType.Wants
                        ? "Seeking Acquisition" : "Available",
                    Notes = SelectedInterestType == MemberBlueprintInterestType.Wants
                        ? "Marked as wanted from Blueprint Explorer"
                        : "Registered from Blueprint Explorer"
                });

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("RegisterOwnership response. StatusCode={StatusCode} Body={Body}",
                (int)response.StatusCode, TrimForDisplay(body));

            response.EnsureSuccessStatusCode();
            await LoadBlueprintDetailAsync(SelectedBlueprintDetail.Id);

            StatusMessage = SelectedInterestType == MemberBlueprintInterestType.Wants
                ? $"Marked {SelectedBlueprintDetail.BlueprintName} as wanted."
                : $"Ownership registered for {SelectedBlueprintDetail.BlueprintName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save blueprint status.");
            StatusMessage = $"Failed to save blueprint status: {ex.GetType().Name} — {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MarkWantedAsync()
    {
        SelectedInterestType = MemberBlueprintInterestType.Wants;
        await RegisterOwnershipAsync();
    }

    [RelayCommand]
    private async Task MarkOwnedAsync()
    {
        SelectedInterestType = MemberBlueprintInterestType.Owns;
        await RegisterOwnershipAsync();
    }

    [RelayCommand]
    private async Task CreateCraftingRequestAsync()
    {
        if (SelectedBlueprintDetail is null)
        {
            StatusMessage = "Select a blueprint before creating a crafting request.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Creating crafting request...";

            using var client = _apiClientProvider.CreateClient();
            using var response = await client.PostAsJsonAsync(
                "api/v1/crafting/requests",
                new CreateCraftingRequestDto
                {
                    BlueprintId = SelectedBlueprintDetail.Id,
                    QuantityRequested = 1,
                    Priority = RequestPriority.Normal,
                    DeliveryLocation = "House Wolf Coordination",
                    Notes = BuildCraftingRequestNote(SelectedBlueprintDetail)
                });

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("CreateCraftingRequest response. StatusCode={StatusCode} Body={Body}",
                (int)response.StatusCode, TrimForDisplay(body));

            response.EnsureSuccessStatusCode();
            StatusMessage = $"Crafting request created for {SelectedBlueprintDetail.CraftedItemName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create crafting request.");
            StatusMessage = $"Failed to create crafting request: {ex.GetType().Name} — {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateProcurementRequestAsync()
    {
        if (SelectedBlueprintDetail is null)
        {
            StatusMessage = "Select a blueprint before creating a procurement request.";
            return;
        }

        if (SelectedMaterial is null)
        {
            StatusMessage = "Select a recipe material before creating a procurement request.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Creating procurement request...";

            using var client = _apiClientProvider.CreateClient();
            using var response = await client.PostAsJsonAsync(
                "api/v1/crafting/procurement-requests",
                new CreateMaterialProcurementRequestDto
                {
                    MaterialId = SelectedMaterial.MaterialId,
                    QuantityRequested = (decimal)(SelectedMaterial.QuantityRequired > 0
                        ? SelectedMaterial.QuantityRequired
                        : SelectedMaterial.Quantity),
                    PreferredForm = MaterialFormPreference.Any,
                    Priority = RequestPriority.Normal,
                    DeliveryLocation = "House Wolf Coordination",
                    NumberOfHelpersNeeded = 1,
                    Notes = BuildProcurementRequestNote(SelectedBlueprintDetail, SelectedMaterial)
                });

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("CreateProcurementRequest response. StatusCode={StatusCode} Body={Body}",
                (int)response.StatusCode, TrimForDisplay(body));

            response.EnsureSuccessStatusCode();
            StatusMessage = $"Procurement request created for {SelectedMaterial.MaterialName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create procurement request.");
            StatusMessage = $"Failed to create procurement request: {ex.GetType().Name} — {ex.Message}";
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
            .SelectMany(component => component.Modifiers)
            .GroupBy(modifier => modifier.StatName);

        foreach (var group in grouped)
        {
            var combined = group.Sum(modifier => modifier.Percentage * (modifier.ParentComponent.QualityValue / 1000.0));
            CombinedModifiers.Add(new BlueprintComponentModifierPreview
            {
                PropertyKey = group.Key,
                CalculatedValue = combined
            });
        }
    }

    // ─── HELPERS ────────────────────────────────────────────────────────────────

    private static bool IsJsonResponse(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mediaType, "text/json", StringComparison.OrdinalIgnoreCase)
               || (mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string TrimForDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "<empty response>";
        var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 300 ? compact : compact[..300] + "...";
    }

    private static string BuildCraftingRequestNote(BlueprintDetailDto detail)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Blueprint: {detail.BlueprintName}");
        sb.AppendLine($"Crafted Item: {detail.CraftedItemName}");
        if (!string.IsNullOrWhiteSpace(detail.AcquisitionSummary))
            sb.AppendLine($"Acquisition: {detail.AcquisitionSummary}");
        if (detail.Materials.Count > 0)
        {
            sb.AppendLine("Required materials:");
            foreach (var material in detail.Materials)
                sb.AppendLine($"- {material.MaterialName}: {(material.QuantityRequired > 0 ? material.QuantityRequired : material.Quantity):N2} {material.Unit}");
        }
        return sb.ToString().Trim();
    }

    private static string BuildProcurementRequestNote(BlueprintDetailDto detail, BlueprintRecipeMaterialDto material)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Blueprint support request for: {detail.BlueprintName}");
        sb.AppendLine($"Crafted item: {detail.CraftedItemName}");
        sb.AppendLine($"Requested material: {material.MaterialName}");
        sb.AppendLine($"Required quantity: {(material.QuantityRequired > 0 ? material.QuantityRequired : material.Quantity):N2} {material.Unit}");
        if (!string.IsNullOrWhiteSpace(detail.AcquisitionSummary))
            sb.AppendLine($"Blueprint acquisition: {detail.AcquisitionSummary}");
        return sb.ToString().Trim();
    }
}