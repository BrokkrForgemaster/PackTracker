using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class BlueprintExplorerView : UserControl
{
    public BlueprintExplorerView(BlueprintExplorerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
