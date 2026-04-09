using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PackTracker.Presentation.ViewModels;

public partial class ComponentViewModel : ObservableObject
{
    [ObservableProperty] private string partName = "Unknown Part";
    [ObservableProperty] private string materialName = "Unknown Material";
    [ObservableProperty] private double scu;
    [ObservableProperty] private int quantity = 1;

    private int _qualityValue = 1000;

    public int QualityValue
    {
        get => _qualityValue;
        set
        {
            if (SetProperty(ref _qualityValue, value))
            {
                foreach (var mod in Modifiers)
                    mod.NotifyEffectiveValueChanged();

                Parent?.UpdateCombinedModifiers();
            }
        }
    }

    public ObservableCollection<StatModifier> Modifiers { get; } = new();

    public BlueprintExplorerViewModel? Parent { get; set; }

    public string ScuDisplay => $"{Scu:0.0000} SCU";
}