using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PackTracker.Presentation.ViewModels;

public partial class ComponentViewModel : ObservableObject
{
    [ObservableProperty] private string partName = "Unknown Part";
    [ObservableProperty] private string materialName = "Unknown Material";
    [ObservableProperty] private double quantity;
    [ObservableProperty] private int qualityValue = 500; // Default 500

    // This list will hold the green/red percentage pills
    public ObservableCollection<StatModifier> Modifiers { get; } = new();

    partial void OnQualityValueChanged(int value) => Parent?.UpdateCombinedModifiers();

    public BlueprintExplorerViewModel? Parent { get; set; }
}

public record StatModifier(string StatName, double Percentage, ComponentViewModel ParentComponent);
