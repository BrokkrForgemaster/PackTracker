using System.Windows;
using System.Windows.Input;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class MedalDetailWindow : Window
{
    public MedalDetailWindow(AdminMedalDefinitionDto medal, Window owner)
    {
        InitializeComponent();
        Owner = owner;
        ApplyScreenSize();
        DataContext = MedalDetailModel.FromMedal(medal);
    }

    public MedalDetailWindow(RibbonEntry ribbon, Window owner)
    {
        InitializeComponent();
        Owner = owner;
        ApplyScreenSize();
        DataContext = MedalDetailModel.FromRibbon(ribbon);
    }

    private void ApplyScreenSize()
    {
        var workArea = SystemParameters.WorkArea;
        Width  = Math.Clamp(workArea.Width  * 0.50, 520, 1000);
        Height = Math.Clamp(workArea.Height * 0.65, 440,  820);
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

internal sealed class MedalDetailModel
{
    public string Name { get; }
    public string Description { get; }
    public string? PackUri { get; }
    public string? AwardCountText { get; }
    public string? SourceSystem { get; }
    public Visibility ImageVisibility { get; }
    public Visibility MetaVisibility { get; }
    public Visibility AwardCountVisibility { get; }
    public Visibility SourceVisibility { get; }

    public static MedalDetailModel FromMedal(AdminMedalDefinitionDto medal) => new(
        medal.Name,
        medal.Description,
        ResolvePackUri(medal.ImagePath),
        medal.AwardCount,
        medal.SourceSystem);

    public static MedalDetailModel FromRibbon(RibbonEntry ribbon) => new(
        ribbon.Name,
        ribbon.Description,
        ribbon.PackUri,
        null,
        null);

    private MedalDetailModel(string name, string description, string? packUri, int? awardCount, string? sourceSystem)
    {
        Name = name;
        Description = description;
        PackUri = packUri;
        AwardCountText = awardCount.HasValue
            ? $"{awardCount} award{(awardCount == 1 ? "" : "s")}"
            : null;
        SourceSystem = sourceSystem;
        ImageVisibility = packUri != null ? Visibility.Visible : Visibility.Collapsed;
        AwardCountVisibility = AwardCountText != null ? Visibility.Visible : Visibility.Collapsed;
        SourceVisibility = sourceSystem != null ? Visibility.Visible : Visibility.Collapsed;
        MetaVisibility = (AwardCountText != null || sourceSystem != null) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string? ResolvePackUri(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("../") || normalized.StartsWith("./"))
            normalized = normalized[(normalized.IndexOf('/') + 1)..];
        return $"pack://application:,,,/{normalized}";
    }
}
