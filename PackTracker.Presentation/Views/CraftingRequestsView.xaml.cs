using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class CraftingRequestsView : UserControl
{
    public CraftingRequestsView(CraftingRequestsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
