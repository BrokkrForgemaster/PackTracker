using System;
using System.ComponentModel;
using System.Globalization;

namespace PackTracker.Presentation.ViewModels;

public class StatModifier : INotifyPropertyChanged
{
    private static readonly TextInfo TextInfo = CultureInfo.CurrentCulture.TextInfo;

    public string StatName { get; }
    public double AtMinQuality { get; }
    public double AtMaxQuality { get; }
    public ComponentViewModel ParentComponent { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName => TextInfo.ToTitleCase(
        StatName
            .Replace("weapon_", "")
            .Replace("firerate", "fire rate")
            .Replace("_", " ")
            .Trim());

    public double EffectiveValue => EffectiveAtQuality(ParentComponent.QualityValue);

    public bool IsPositive => GetDisplayValue(EffectiveValue) > 0;
    public bool IsNegative => GetDisplayValue(EffectiveValue) < 0;

    /// <summary>
    /// Some stats are better when the raw value is lower.
    /// These should have their displayed sign flipped for the UI.
    /// </summary>
    public bool IsInverseBenefitStat =>
        StatName.Contains("recoil", StringComparison.OrdinalIgnoreCase) ||
        StatName.Contains("spread", StringComparison.OrdinalIgnoreCase) ||
        StatName.Contains("sway", StringComparison.OrdinalIgnoreCase) ||
        StatName.Contains("bloom", StringComparison.OrdinalIgnoreCase) ||
        StatName.Contains("kick", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Raw effective value from source data.
    /// </summary>
    public double EffectiveAtQuality(int quality)
    {
        double t = Math.Clamp(quality / 1000.0, 0.0, 1.0);

        // Current interpolation
        double multiplier = AtMinQuality + ((AtMaxQuality - AtMinQuality) * t);

        double result = (multiplier - 1.0) * 100.0;

        System.Diagnostics.Debug.WriteLine(
            $"Stat={StatName}, Quality={quality}, AtMin={AtMinQuality}, AtMax={AtMaxQuality}, Multiplier={multiplier}, Percent={result}");

        return result;
    }

    /// <summary>
    /// Value shown to the user. Inverse-benefit stats flip sign so improvements display positively.
    /// </summary>
    public double DisplayValue => GetDisplayValue(EffectiveValue);

    private double GetDisplayValue(double rawValue)
    {
        return IsInverseBenefitStat ? -rawValue : rawValue;
    }

    public void NotifyEffectiveValueChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveValue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayValue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPositive)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNegative)));
    }

    public StatModifier(
        string statName,
        double atMinQuality,
        double atMaxQuality,
        ComponentViewModel parentComponent)
    {
        StatName = statName;
        AtMinQuality = atMinQuality;
        AtMaxQuality = atMaxQuality;
        ParentComponent = parentComponent;
    }
}