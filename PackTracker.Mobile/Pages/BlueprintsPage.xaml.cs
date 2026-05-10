using System.Collections.ObjectModel;
using System.Globalization;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Domain.Enums;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class BlueprintsPage : ContentPage
{
    private readonly PackTrackerApiClient _api;
    private readonly ObservableCollection<BlueprintCard> _blueprints = new();
    private readonly HashSet<Guid> _ownedBlueprintIds = new();
    private BlueprintDetailDto? _selectedBlueprintDetail;
    private BlueprintCard? _selectedCard;

    public BlueprintsPage(PackTrackerApiClient api)
    {
        InitializeComponent();
        _api = api;
        BlueprintsView.ItemsSource = _blueprints;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadOwnedBlueprintsAsync().ConfigureAwait(false);
    }

    private async void SearchButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Searching blueprints...";
            var term = Uri.EscapeDataString(SearchEntry.Text ?? string.Empty);
            var items = await _api.GetAsync<List<BlueprintSearchItemDto>>(
                $"api/v1/blueprints?q={term}&inGameOnly=true").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _blueprints.Clear();
                if (items is null || items.Count == 0)
                {
                    StatusLabel.Text = "No blueprints matched your search.";
                    return;
                }

                foreach (var item in items)
                    _blueprints.Add(new BlueprintCard(item));

                StatusLabel.Text = $"Loaded {_blueprints.Count} blueprints.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Blueprint search failed: {ex.Message}";
            });
        }
    }

    private async void BlueprintsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not BlueprintCard card)
            return;

        _selectedCard = card;
        await LoadBlueprintDetailAsync(card.Id).ConfigureAwait(false);
    }

    private async Task LoadBlueprintDetailAsync(Guid blueprintId)
    {
        try
        {
            StatusLabel.Text = "Loading blueprint detail...";
            var detail = await _api.GetAsync<BlueprintDetailDto>($"api/v1/blueprints/{blueprintId}").ConfigureAwait(false);
            _selectedBlueprintDetail = detail;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (detail is null)
                {
                    DetailNameLabel.Text = "Blueprint detail unavailable.";
                    DetailMetaLabel.Text = string.Empty;
                    DetailDescriptionLabel.Text = string.Empty;
                    DetailMaterialsLabel.Text = string.Empty;
                    SetDetailButtons(false, false);
                    StatusLabel.Text = "Could not load blueprint detail.";
                    return;
                }

                DetailNameLabel.Text = string.IsNullOrWhiteSpace(detail.CraftedItemName)
                    ? detail.BlueprintName
                    : $"{detail.CraftedItemName} ({detail.BlueprintName})";
                DetailMetaLabel.Text =
                    $"{detail.Category} • Owners {detail.OwnerCount} • Output {detail.OutputQuantity} • {detail.DataConfidence}";
                DetailDescriptionLabel.Text = string.IsNullOrWhiteSpace(detail.Description)
                    ? detail.AcquisitionSummary ?? "No description."
                    : detail.Description;
                DetailMaterialsLabel.Text = detail.Materials.Count == 0
                    ? "No materials listed."
                    : "Materials: " + string.Join(", ", detail.Materials.Select(x => $"{x.MaterialName} x{x.QuantityRequired:0.##}"));

                var isOwned = _ownedBlueprintIds.Contains(detail.Id) || _ownedBlueprintIds.Contains(detail.WikiUuid);
                SetDetailButtons(true, isOwned);
                StatusLabel.Text = "Blueprint detail loaded.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Blueprint detail failed: {ex.Message}";
            });
        }
    }

    private async void MarkOwnedButton_Clicked(object sender, EventArgs e)
    {
        if (_selectedBlueprintDetail is null)
            return;

        try
        {
            using var response = await _api.PostAsync(
                $"api/v1/blueprints/{GetOperationBlueprintId()}/ownership",
                new RegisterBlueprintOwnershipRequest
                {
                    InterestType = MemberBlueprintInterestType.Owns,
                    AvailabilityStatus = "Available"
                }).ConfigureAwait(false);

            StatusLabel.Text = await _api.ReadMessageAsync(response).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await LoadOwnedBlueprintsAsync().ConfigureAwait(false);
                await LoadBlueprintDetailAsync(GetOperationBlueprintId()).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Mark owned failed: {ex.Message}");
        }
    }

    private async void RemoveOwnedButton_Clicked(object sender, EventArgs e)
    {
        if (_selectedBlueprintDetail is null)
            return;

        try
        {
            using var response = await _api.DeleteAsync(
                $"api/v1/blueprints/{GetOperationBlueprintId()}/ownership").ConfigureAwait(false);

            StatusLabel.Text = await _api.ReadMessageAsync(response).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await LoadOwnedBlueprintsAsync().ConfigureAwait(false);
                await LoadBlueprintDetailAsync(GetOperationBlueprintId()).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Remove owned failed: {ex.Message}");
        }
    }

    private async void CreateCraftingButton_Clicked(object sender, EventArgs e)
    {
        if (_selectedBlueprintDetail is null)
            return;

        var quantityText = await DisplayPromptAsync("Crafting Request", "Quantity requested", initialValue: "1", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(quantityText) || !int.TryParse(quantityText, out var quantity) || quantity < 1)
        {
            StatusLabel.Text = "Crafting request cancelled.";
            return;
        }

        var qualityText = await DisplayPromptAsync("Crafting Request", "Minimum quality", initialValue: "500", keyboard: Keyboard.Numeric);
        var minimumQuality = int.TryParse(qualityText, out var parsedQuality) && parsedQuality >= 1
            ? parsedQuality
            : 500;

        var deliveryLocation = await DisplayPromptAsync("Crafting Request", "Delivery location", initialValue: string.Empty);
        var notes = await DisplayPromptAsync("Crafting Request", "Notes", initialValue: string.Empty);

        try
        {
            using var response = await _api.PostAsync(
                "api/v1/crafting/requests",
                new CreateCraftingRequestDto
                {
                    BlueprintId = GetOperationBlueprintId(),
                    CraftedItemName = _selectedBlueprintDetail.CraftedItemName,
                    QuantityRequested = quantity,
                    MinimumQuality = minimumQuality,
                    Priority = RequestPriority.Normal,
                    MaterialSupplyMode = MaterialSupplyMode.Negotiable,
                    DeliveryLocation = deliveryLocation,
                    Notes = notes,
                    RewardOffered = "Negotiable",
                    RequesterTimeZoneDisplayName = TimeZoneInfo.Local.DisplayName,
                    RequesterUtcOffsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.Now.UtcDateTime).TotalMinutes
                }).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                StatusLabel.Text = await _api.ReadMessageAsync(response).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    await Shell.Current.GoToAsync("//CraftingQueue");
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Create crafting failed: {ex.Message}");
        }
    }

    private async void CreateProcurementButton_Clicked(object sender, EventArgs e)
    {
        if (_selectedBlueprintDetail is null || _selectedBlueprintDetail.Materials.Count == 0)
        {
            StatusLabel.Text = "This blueprint has no recipe materials.";
            return;
        }

        var qualityText = await DisplayPromptAsync("Procurement Requests", "Minimum quality for all materials", initialValue: "500", keyboard: Keyboard.Numeric);
        var minimumQuality = int.TryParse(qualityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedQuality) && parsedQuality >= 1
            ? parsedQuality
            : 500;

        try
        {
            var successCount = 0;
            foreach (var material in _selectedBlueprintDetail.Materials)
            {
                using var response = await _api.PostAsync(
                    "api/v1/crafting/procurement-requests",
                    new CreateMaterialProcurementRequestDto
                    {
                        MaterialId = material.MaterialId,
                        MaterialName = material.MaterialName,
                        QuantityRequested = (decimal)material.QuantityRequired,
                        MinimumQuality = minimumQuality,
                        Priority = RequestPriority.Normal,
                        PreferredForm = MaterialFormPreference.Any,
                        RewardOffered = "Negotiable"
                    }).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    successCount++;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                StatusLabel.Text = $"Created {successCount}/{_selectedBlueprintDetail.Materials.Count} procurement requests.";
                if (successCount > 0)
                    await Shell.Current.GoToAsync("//ProcurementQueue");
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Create procurement failed: {ex.Message}");
        }
    }

    private async Task LoadOwnedBlueprintsAsync()
    {
        var items = await _api.GetAsync<List<OwnedBlueprintSummaryDto>>("api/v1/blueprints/owned").ConfigureAwait(false);

        _ownedBlueprintIds.Clear();
        if (items is null)
            return;

        foreach (var item in items)
        {
            _ownedBlueprintIds.Add(item.BlueprintId);
            if (item.WikiUuid != Guid.Empty)
                _ownedBlueprintIds.Add(item.WikiUuid);
        }
    }

    private Guid GetOperationBlueprintId()
    {
        if (_selectedBlueprintDetail is null)
            return Guid.Empty;

        return _selectedBlueprintDetail.Id != Guid.Empty
            ? _selectedBlueprintDetail.Id
            : _selectedBlueprintDetail.WikiUuid;
    }

    private void SetDetailButtons(bool enabled, bool isOwned)
    {
        MarkOwnedButton.IsEnabled = enabled && !isOwned;
        RemoveOwnedButton.IsEnabled = enabled && isOwned;
        CreateCraftingButton.IsEnabled = enabled;
        CreateProcurementButton.IsEnabled = enabled;
    }

    private sealed class BlueprintCard
    {
        public BlueprintCard(BlueprintSearchItemDto dto)
        {
            Id = dto.Id;
            BlueprintName = dto.BlueprintName;
            CraftedItemName = dto.CraftedItemName;
            Summary = $"{dto.Category} • Owners {dto.VerifiedOwnerCount} • {dto.DataConfidence}";
        }

        public Guid Id { get; }
        public string BlueprintName { get; }
        public string CraftedItemName { get; }
        public string Summary { get; }
    }
}
