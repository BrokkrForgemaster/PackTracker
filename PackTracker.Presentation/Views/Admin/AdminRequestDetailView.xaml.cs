using System.Windows.Controls;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminRequestDetailView : UserControl
{
    private readonly AdminRequestDetailViewModel _viewModel;

    public AdminRequestDetailView(AdminRequestDetailViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    public Task LoadAsync(Guid id, string requestType) =>
        _viewModel.LoadAsync(id, requestType);

    public event Action? BackRequested;

    private void Back_Click(object sender, System.Windows.RoutedEventArgs e) =>
        BackRequested?.Invoke();
}
