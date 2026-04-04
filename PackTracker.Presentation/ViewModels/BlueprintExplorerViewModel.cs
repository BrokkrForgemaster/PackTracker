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

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string categoryFilter = string.Empty;
    [ObservableProperty] private bool inGameOnly = true;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Search for an in-game blueprint to begin.";
    [ObservableProperty] private BlueprintSearchItemDto? selectedBlueprint;
    [ObservableProperty] private BlueprintDetailDto? selectedBlueprintDetail;
    [ObservableProperty] private BlueprintRecipeMaterialDto? selectedMaterial;
    [ObservableProperty] private MemberBlueprintInterestType selectedInterestType = MemberBlueprintInterestType.Owns;

    public IReadOnlyList<MemberBlueprintInterestType> InterestTypeOptions { get; } =
        Enum.GetValues<MemberBlueprintInterestType>();

    public BlueprintExplorerViewModel(IApiClientProvider apiClientProvider, ILogger<BlueprintExplorerViewModel> logger)
    {
        _apiClientProvider = apiClientProvider;
        _logger = logger;
        SearchText = "Blueprint";
        _ = SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Searching blueprint catalog...";

            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(SearchText))
                query.Add($"q={Uri.EscapeDataString(SearchText.Trim())}");
            if (!string.IsNullOrWhiteSpace(CategoryFilter))
                query.Add($"category={Uri.EscapeDataString(CategoryFilter.Trim())}");
            query.Add($"inGameOnly={InGameOnly.ToString().ToLowerInvariant()}");

            var url = "api/v1/blueprints";
            if (query.Count > 0)
                url += "?" + string.Join("&", query);

            using var client = _apiClientProvider.CreateClient();
            var requestUri = new Uri(client.BaseAddress!, url);
            _logger.LogInformation("Blueprint search request starting. BaseAddress={BaseAddress} RelativeUrl={RelativeUrl} AbsoluteUrl={AbsoluteUrl}", client.BaseAddress, url, requestUri);

            using var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Blueprint search response received. StatusCode={StatusCode} ContentType={ContentType} BodyPreview={BodyPreview}",
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType ?? "<none>",
                TrimForDisplay(body));

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Blueprint search failed ({(int)response.StatusCode}): {TrimForDisplay(body)}";
                Results.Clear();
                return;
            }

            if (!IsJsonResponse(response))
            {
                StatusMessage = $"Blueprint search returned non-JSON content. First response text: {TrimForDisplay(body)}";
                Results.Clear();
                return;
            }

            var items = JsonSerializer.Deserialize<List<BlueprintSearchItemDto>>(body,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<BlueprintSearchItemDto>();

            Results.Clear();
            foreach (var item in items)
                Results.Add(item);

            StatusMessage = Results.Count == 0
                ? "No blueprints matched your search."
                : $"Loaded {Results.Count} blueprint results.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blueprint search failed.");
            StatusMessage = $"Blueprint search failed: {ex.Message}";
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
            var relativeUrl = $"api/v1/blueprints/{blueprintId}";
            var requestUri = new Uri(client.BaseAddress!, relativeUrl);
            _logger.LogInformation("Blueprint detail request starting. BaseAddress={BaseAddress} RelativeUrl={RelativeUrl} AbsoluteUrl={AbsoluteUrl}", client.BaseAddress, relativeUrl, requestUri);

            using var response = await client.GetAsync(relativeUrl);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Blueprint detail response received. StatusCode={StatusCode} ContentType={ContentType} BodyPreview={BodyPreview}",
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType ?? "<none>",
                TrimForDisplay(body));

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Blueprint detail failed ({(int)response.StatusCode}): {TrimForDisplay(body)}";
                SelectedBlueprintDetail = null;
                Materials.Clear();
                Owners.Clear();
                return;
            }

            if (!IsJsonResponse(response))
            {
                StatusMessage = $"Blueprint detail returned non-JSON content. First response text: {TrimForDisplay(body)}";
                SelectedBlueprintDetail = null;
                Materials.Clear();
                Owners.Clear();
                return;
            }

            var detail = JsonSerializer.Deserialize<BlueprintDetailDto>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            SelectedBlueprintDetail = detail;
            Materials.Clear();
            Owners.Clear();
            SelectedMaterial = null;

            foreach (var material in detail?.Materials ?? Array.Empty<BlueprintRecipeMaterialDto>())
                Materials.Add(material);
            foreach (var owner in detail?.Owners ?? Array.Empty<BlueprintOwnerDto>())
                Owners.Add(owner);

            StatusMessage = detail is null
                ? "Blueprint detail not found."
                : $"Loaded {detail.BlueprintName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blueprint detail for {BlueprintId}", blueprintId);
            StatusMessage = $"Failed to load blueprint detail: {ex.Message}";
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
                        ? "Seeking Acquisition"
                        : "Available",
                    Notes = SelectedInterestType == MemberBlueprintInterestType.Wants
                        ? "Marked as wanted from Blueprint Explorer"
                        : "Registered from Blueprint Explorer"
                });

            response.EnsureSuccessStatusCode();
            await LoadBlueprintDetailAsync(SelectedBlueprintDetail.Id);
            StatusMessage = SelectedInterestType == MemberBlueprintInterestType.Wants
                ? $"Marked {SelectedBlueprintDetail.BlueprintName} as wanted."
                : $"Ownership registered for {SelectedBlueprintDetail.BlueprintName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save blueprint status.");
            StatusMessage = $"Failed to save blueprint status: {ex.Message}";
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

            response.EnsureSuccessStatusCode();
            StatusMessage = $"Crafting request created for {SelectedBlueprintDetail.CraftedItemName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create crafting request.");
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
                    QuantityRequested = SelectedMaterial.QuantityRequired > 0 ? SelectedMaterial.QuantityRequired : SelectedMaterial.Quantity,
                    PreferredForm = MaterialFormPreference.Any,
                    Priority = RequestPriority.Normal,
                    DeliveryLocation = "House Wolf Coordination",
                    NumberOfHelpersNeeded = 1,
                    Notes = BuildProcurementRequestNote(SelectedBlueprintDetail, SelectedMaterial)
                });

            response.EnsureSuccessStatusCode();
            StatusMessage = $"Procurement request created for {SelectedMaterial.MaterialName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create procurement request.");
            StatusMessage = $"Failed to create procurement request: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static bool IsJsonResponse(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mediaType, "text/json", StringComparison.OrdinalIgnoreCase)
               || (mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string TrimForDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty response>";

        var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 180 ? compact : compact[..180] + "...";
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
