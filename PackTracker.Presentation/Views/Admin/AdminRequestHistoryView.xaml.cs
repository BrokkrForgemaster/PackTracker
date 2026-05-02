using System.Windows.Controls;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminRequestHistoryView : UserControl
{
    private readonly AdminRequestHistoryViewModel _viewModel;

    public AdminRequestHistoryView(AdminRequestHistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }
}
