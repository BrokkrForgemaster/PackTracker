using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class ProcurementRequestsView : UserControl
{
    public ProcurementRequestsView(ProcurementRequestsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
