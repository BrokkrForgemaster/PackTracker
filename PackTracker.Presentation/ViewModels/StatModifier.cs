namespace PackTracker.Presentation.ViewModels;

/// <summary>
/// Represents a stat modifier applied to a component.
/// </summary>
public class StatModifier
{
    public string StatName { get; }
    public double BaseValue { get; }
    public ComponentViewModel ParentComponent { get; }

    public StatModifier(string statName, double baseValue, ComponentViewModel parentComponent)
    {
        StatName = statName;
        BaseValue = baseValue;
        ParentComponent = parentComponent;
    }
}

