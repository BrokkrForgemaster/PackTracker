using System.Windows.Controls;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminDashboardView : UserControl
{
    private readonly AdminDashboardViewModel _viewModel;

    public AdminDashboardView(AdminDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }
}
