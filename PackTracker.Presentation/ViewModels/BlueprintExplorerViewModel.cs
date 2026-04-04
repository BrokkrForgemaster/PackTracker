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

public partial class BlueprintExplorerViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly ILogger<BlueprintExplorerViewModel> _logger;

    public ObservableCollection<BlueprintSearchItemDto> Results { get; } = new();
    public ObservableCollection<BlueprintRecipeMaterialDto> Materials { get; } = new();
    public ObservableCollection<BlueprintOwnerDto> Owners { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private bool inGameOnly = true;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Search for an in-game blueprint to begin.";
    [ObservableProperty] private BlueprintSearchItemDto? selectedBlueprint;
    [ObservableProperty] private BlueprintDetailDto? selectedBlueprintDetail;
    [ObservableProperty] private BlueprintRecipeMaterialDto? selectedMaterial;
    [ObservableProperty] private MemberBlueprintInterestType selectedInterestType = MemberBlueprintInterestType.Owns;

    private const string AllCategoriesLabel = "All Categories";

    public IReadOnlyList<MemberBlueprintInterestType> InterestTypeOptions { get; } =
        Enum.GetValues<MemberBlueprintInterestType>();

    public BlueprintExplorerViewModel(IApiClientProvider apiClientProvider, ILogger<BlueprintExplorerViewModel> logger)
    {
        _apiClientProvider = apiClientProvider;
        _logger = logger;
        SearchText = "Blueprint";
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
        await SearchAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();

            if (client.BaseAddress is null)
            {
                _logger.LogWarning("Cannot load categories — BaseAddress is null.");
                return;
            }

            var relativeUrl = "api/v1/blueprints/categories";
            _logger.LogInformation("Loading blueprint categories from {Url}", relativeUrl);

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(relativeUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load blueprint categories — using empty list.");
                PopulateDefaultCategories();
                return;
            }

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || !IsJsonResponse(response))
            {
                _logger.LogWarning("Categories endpoint returned {StatusCode}. Falling back to defaults.", (int)response.StatusCode);
                PopulateDefaultCategories();
                return;
            }

            var fetched = JsonSerializer.Deserialize<List<string>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<string>();

            Categories.Clear();
            Categories.Add(AllCategoriesLabel);
            foreach (var cat in fetched.Where(c => !string.IsNullOrWhiteSpace(c)).OrderBy(c => c))
                Categories.Add(cat);

            SelectedCategory = AllCategoriesLabel;
            _logger.LogInformation("Loaded {Count} blueprint categories.", Categories.Count - 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading categories.");
            PopulateDefaultCategories();
        }
    }

    private void PopulateDefaultCategories()
    {
        Categories.Clear();
        Categories.Add(AllCategoriesLabel);
        SelectedCategory = AllCategoriesLabel;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Searching blueprint catalog...";

            using var client = _apiClientProvider.CreateClient();

            _logger.LogInformation("ApiClient BaseAddress={BaseAddress}", client.BaseAddress?.ToString() ?? "<null>");

            if (client.BaseAddress is null)
            {
                StatusMessage = "API client has no BaseAddress configured. Check IApiClientProvider setup.";
                _logger.LogError("HttpClient BaseAddress is null. Cannot build request URI.");
                return;
            }

            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(SearchText))
                query.Add($"q={Uri.EscapeDataString(SearchText.Trim())}");

            var effectiveCategory = SelectedCategory == AllCategoriesLabel ? null : SelectedCategory;
            if (!string.IsNullOrWhiteSpace(effectiveCategory))
                query.Add($"category={Uri.EscapeDataString(effectiveCategory.Trim())}");

            query.Add($"inGameOnly={InGameOnly.ToString().ToLowerInvariant()}");

            var relativeUrl = "api/v1/blueprints";
            if (query.Count > 0)
                relativeUrl += "?" + string.Join("&", query);

            var requestUri = new Uri(client.BaseAddress, relativeUrl);
            _logger.LogInformation("Blueprint search request. RelativeUrl={RelativeUrl} AbsoluteUrl={AbsoluteUrl}", relativeUrl, requestUri);

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(relativeUrl);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Blueprint search HTTP request failed.");
                StatusMessage = $"Network error — could not reach API: {ex.Message}";
                Results.Clear();
                return;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Blueprint search request timed out.");
                StatusMessage = "Blueprint search timed out. Check that the API is running.";
                Results.Clear();
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "<none>";

            _logger.LogInformation(
                "Blueprint search response. StatusCode={StatusCode} ContentType={ContentType} BodyPreview={BodyPreview}",
                (int)response.StatusCode, contentType, TrimForDisplay(body));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Blueprint search returned non-success. StatusCode={StatusCode} Body={Body}",
                    (int)response.StatusCode, body);
                StatusMessage = IsJsonResponse(response)
                    ? $"Blueprint search failed ({(int)response.StatusCode}): {TrimForDisplay(body)}"
                    : $"Blueprint search failed ({(int)response.StatusCode}). API returned {contentType}. Preview: {TrimForDisplay(body)}";
                Results.Clear();
                return;
            }

            if (!IsJsonResponse(response))
            {
                _logger.LogError("Blueprint search returned non-JSON. ContentType={ContentType} BodyPreview={BodyPreview}",
                    contentType, TrimForDisplay(body));
                StatusMessage = $"Blueprint search returned {contentType} instead of JSON. Preview: {TrimForDisplay(body)}";
                Results.Clear();
                return;
            }

            List<BlueprintSearchItemDto> items;
            try
            {
                items = JsonSerializer.Deserialize<List<BlueprintSearchItemDto>>(body,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<BlueprintSearchItemDto>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Blueprint search JSON deserialization failed.");
                StatusMessage = $"Blueprint search returned malformed JSON: {ex.Message}";
                Results.Clear();
                return;
            }

            Results.Clear();
            foreach (var item in items)
                Results.Add(item);

            StatusMessage = Results.Count == 0
                ? "No blueprints matched your search."
                : $"Loaded {Results.Count} blueprint results.";

            _logger.LogInformation("Blueprint search complete. ResultCount={ResultCount}", Results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint search encountered an unexpected error.");
            StatusMessage = $"Unexpected error during blueprint search: {ex.GetType().Name} — {ex.Message}";
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

    private async Task LoadBlueprintDetailAsync(Guid blueprintId)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading blueprint detail...";

            using var client = _apiClientProvider.CreateClient();

            if (client.BaseAddress is null)
            {
                StatusMessage = "API client has no BaseAddress configured.";
                return;
            }

            var relativeUrl = $"api/v1/blueprints/{blueprintId}";
            _logger.LogInformation("Blueprint detail request. RelativeUrl={RelativeUrl}", relativeUrl);

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(relativeUrl);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Blueprint detail HTTP request failed.");
                StatusMessage = $"Network error — could not reach API: {ex.Message}";
                return;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Blueprint detail request timed out.");
                StatusMessage = "Blueprint detail request timed out.";
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "<none>";

            _logger.LogInformation("Blueprint detail response. StatusCode={StatusCode} ContentType={ContentType} BodyPreview={BodyPreview}",
                (int)response.StatusCode, contentType, TrimForDisplay(body));

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = IsJsonResponse(response)
                    ? $"Blueprint detail failed ({(int)response.StatusCode}): {TrimForDisplay(body)}"
                    : $"Blueprint detail failed ({(int)response.StatusCode}). API returned {contentType}. Preview: {TrimForDisplay(body)}";
                SelectedBlueprintDetail = null;
                Materials.Clear();
                Owners.Clear();
                return;
            }

            if (!IsJsonResponse(response))
            {
                StatusMessage = $"Blueprint detail returned {contentType} instead of JSON. Preview: {TrimForDisplay(body)}";
                SelectedBlueprintDetail = null;
                Materials.Clear();
                Owners.Clear();
                return;
            }

            BlueprintDetailDto? detail;
            try
            {
                detail = JsonSerializer.Deserialize<BlueprintDetailDto>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Blueprint detail JSON deserialization failed.");
                StatusMessage = $"Blueprint detail returned malformed JSON: {ex.Message}";
                SelectedBlueprintDetail = null;
                Materials.Clear();
                Owners.Clear();
                return;
            }

            SelectedBlueprintDetail = detail;
            Materials.Clear();
            Owners.Clear();
            SelectedMaterial = null;

            foreach (var material in detail?.Materials ?? Array.Empty<BlueprintRecipeMaterialDto>())
                Materials.Add(material);
            foreach (var owner in detail?.Owners ?? Array.Empty<BlueprintOwnerDto>())
                Owners.Add(owner);

            StatusMessage = detail is null ? "Blueprint detail not found." : $"Loaded {detail.BlueprintName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint detail encountered an unexpected error for {BlueprintId}", blueprintId);
            StatusMessage = $"Unexpected error loading blueprint detail: {ex.GetType().Name} — {ex.Message}";
        }
        finally
        {
            IsLoading = false;
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