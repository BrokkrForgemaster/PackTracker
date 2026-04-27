using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackTracker.Domain.Enums;

namespace PackTracker.Presentation.ViewModels;

public record EnumOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

public partial class CraftingRequestFormViewModel : ObservableObject
{
    public Guid BlueprintId { get; }
    public string BlueprintName { get; }

    [ObservableProperty] private int quantityRequested = 1;
    [ObservableProperty] private int minimumQuality = 500;
    [ObservableProperty] private EnumOption<RequestPriority> selectedPriority;
    [ObservableProperty] private EnumOption<MaterialSupplyMode> selectedMaterialSupplyMode;
    [ObservableProperty] private string? rewardOffered;
    [ObservableProperty] private string? deliveryLocation;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private string? maxClaimsText;
    [ObservableProperty] private string? validationMessage;

    public RequestPriority Priority => SelectedPriority.Value;
    public MaterialSupplyMode MaterialSupplyMode => SelectedMaterialSupplyMode.Value;
    public string RequesterTimeZoneDisplayName =>
        TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now)
            ? TimeZoneInfo.Local.DaylightName
            : TimeZoneInfo.Local.StandardName;
    public int RequesterUtcOffsetMinutes => (int)DateTimeOffset.Now.Offset.TotalMinutes;
    public string SubmissionTimePreview =>
        $"{DateTime.Now:MMM d, yyyy h:mm tt} {RequesterTimeZoneDisplayName}";

    public bool CanSubmit =>
        !string.IsNullOrWhiteSpace(RewardOffered) &&
        !string.IsNullOrWhiteSpace(DeliveryLocation) &&
        QuantityRequested >= 1 &&
        MinimumQuality >= 1 &&
        HasValidMaxClaims;

    public int? MaxClaims =>
        int.TryParse(MaxClaimsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 1
            ? value
            : null;

    private bool HasValidMaxClaims =>
        string.IsNullOrWhiteSpace(MaxClaimsText)
        || (int.TryParse(MaxClaimsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 1);

    public List<EnumOption<RequestPriority>> PriorityOptions { get; } =
    [
        new(RequestPriority.Low,      "Low"),
        new(RequestPriority.Normal,   "Normal"),
        new(RequestPriority.High,     "High"),
        new(RequestPriority.Critical, "Critical")
    ];

    public List<EnumOption<MaterialSupplyMode>> MaterialSupplyModeOptions { get; } =
    [
        new(MaterialSupplyMode.RequesterWillSupply, "I Will Supply Materials"),
        new(MaterialSupplyMode.CrafterMustSupply,   "Crafter Must Supply"),
        new(MaterialSupplyMode.Negotiable,          "Negotiable")
    ];

    public bool Confirmed { get; private set; }

    public event EventHandler? Submitted;
    public event EventHandler? Cancelled;

    public CraftingRequestFormViewModel(Guid blueprintId, string blueprintName)
    {
        BlueprintId = blueprintId;
        BlueprintName = blueprintName;
        selectedPriority = PriorityOptions[1];                    // Normal
        selectedMaterialSupplyMode = MaterialSupplyModeOptions[2]; // Negotiable
    }

    partial void OnRewardOfferedChanged(string? oldValue, string? newValue)
    {
        OnPropertyChanged(nameof(CanSubmit));
        ValidationMessage = null;
    }

    partial void OnDeliveryLocationChanged(string? oldValue, string? newValue)
    {
        OnPropertyChanged(nameof(CanSubmit));
        ValidationMessage = null;
    }

    partial void OnQuantityRequestedChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(CanSubmit));
        ValidationMessage = null;
    }

    partial void OnMinimumQualityChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(CanSubmit));
        ValidationMessage = null;
    }

    partial void OnMaxClaimsTextChanged(string? oldValue, string? newValue)
    {
        OnPropertyChanged(nameof(CanSubmit));
        ValidationMessage = null;
    }

    [RelayCommand]
    private void Submit()
    {
        if (!CanSubmit)
        {
            ValidationMessage = BuildValidationMessage();
            return;
        }

        ValidationMessage = null;
        Confirmed = true;
        Submitted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private string BuildValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(RewardOffered))
            return "Reward offered is required.";

        if (string.IsNullOrWhiteSpace(DeliveryLocation))
            return "Delivery location is required.";

        if (QuantityRequested < 1)
            return "Quantity must be at least 1.";

        if (MinimumQuality < 1)
            return "Minimum quality must be at least 1.";

        if (!HasValidMaxClaims)
            return "Max claims must be blank or a whole number greater than 0.";

        return "Review the request form and try again.";
    }
}
