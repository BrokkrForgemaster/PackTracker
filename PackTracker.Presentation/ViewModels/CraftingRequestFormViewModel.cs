using System.Collections.Generic;
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

    public RequestPriority Priority => SelectedPriority.Value;
    public MaterialSupplyMode MaterialSupplyMode => SelectedMaterialSupplyMode.Value;

    public bool CanSubmit =>
        !string.IsNullOrWhiteSpace(RewardOffered) &&
        !string.IsNullOrWhiteSpace(DeliveryLocation) &&
        QuantityRequested >= 1 &&
        MinimumQuality >= 1;

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

    partial void OnRewardOfferedChanged(string? value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnDeliveryLocationChanged(string? value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnQuantityRequestedChanged(int value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnMinimumQualityChanged(int value) => OnPropertyChanged(nameof(CanSubmit));

    [RelayCommand]
    private void Submit()
    {
        if (!CanSubmit) return;
        Confirmed = true;
        Submitted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
