using System.Windows;
using System.Windows.Input;
using PackTracker.Application.Admin.DTOs;

namespace PackTracker.Presentation.Views.Admin;

public partial class MedalDetailWindow : Window
{
    public MedalDetailWindow(AdminMedalDefinitionDto award, Window owner)
    {
        InitializeComponent();

        Owner = owner;
        ApplyScreenSize();

        DataContext = MedalDetailModel.FromAward(award);
    }

    private void ApplyScreenSize()
    {
        var workArea = SystemParameters.WorkArea;

        Width = Math.Clamp(workArea.Width * 0.50, 520, 1000);
        Height = Math.Clamp(workArea.Height * 0.65, 440, 820);
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

internal sealed class MedalDetailModel
{
    public string Name { get; }
    public string Description { get; }
    public string? PackUri { get; }
    public string AwardType { get; }
    public string? AwardCountText { get; }
    public string? SourceSystem { get; }
    public Visibility ImageVisibility { get; }
    public Visibility MetaVisibility { get; }
    public Visibility AwardCountVisibility { get; }
    public Visibility SourceVisibility { get; }

    public static MedalDetailModel FromAward(AdminMedalDefinitionDto award)
    {
        return new MedalDetailModel(
            award.Name,
            award.Description,
            ResolvePackUri(award.ImagePath),
            award.AwardType,
            award.AwardCount,
            award.SourceSystem);
    }

    private MedalDetailModel(
        string name,
        string description,
        string? packUri,
        string awardType,
        int? awardCount,
        string? sourceSystem)
    {
        Name = name;
        Description = description;
        PackUri = packUri;
        AwardType = string.IsNullOrWhiteSpace(awardType) ? "Award" : awardType;

        AwardCountText = awardCount.HasValue
            ? $"{awardCount} award{(awardCount == 1 ? "" : "s")}"
            : null;

        SourceSystem = sourceSystem;

        ImageVisibility = packUri is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

        AwardCountVisibility = AwardCountText is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

        SourceVisibility = sourceSystem is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

        MetaVisibility = AwardCountText is not null || sourceSystem is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static string? ResolvePackUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalized = path.Replace('\\', '/').Trim();

        while (normalized.StartsWith("../", StringComparison.Ordinal) ||
               normalized.StartsWith("./", StringComparison.Ordinal))
        {
            var slashIndex = normalized.IndexOf('/', StringComparison.Ordinal);

            if (slashIndex < 0 || slashIndex + 1 >= normalized.Length)
                return null;

            normalized = normalized[(slashIndex + 1)..];
        }

        normalized = normalized.TrimStart('/');

        return $"pack://application:,,,/{normalized}";
    }
}