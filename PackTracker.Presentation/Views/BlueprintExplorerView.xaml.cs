using System;
using System.Windows;
using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

/// <summary>
/// Interaction logic for <see cref="BlueprintExplorerView"/>.
/// Hosts the blueprint operations workspace and provides small UI-only handlers
/// for stepped component quality adjustment.
/// </summary>
public partial class BlueprintExplorerView : UserControl
{
    private const int QualityStep = 1;
    private const int MinQuality = 0;
    private const int MaxQuality = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintExplorerView"/> class.
    /// </summary>
    /// <param name="viewModel">The bound blueprint explorer view model.</param>
    public BlueprintExplorerView(BlueprintExplorerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>
    /// Decreases the selected component quality by a fixed step.
    /// </summary>
    private void DecreaseQuality_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ComponentViewModel component })
            return;

        component.QualityValue = Math.Max(MinQuality, component.QualityValue - QualityStep);
    }

    /// <summary>
    /// Increases the selected component quality by a fixed step.
    /// </summary>
    private void IncreaseQuality_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ComponentViewModel component })
            return;

        component.QualityValue = Math.Min(MaxQuality, component.QualityValue + QualityStep);
    }
}