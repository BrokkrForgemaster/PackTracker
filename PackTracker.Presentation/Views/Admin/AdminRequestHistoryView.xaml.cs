using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminRequestHistoryView : UserControl
{
    private readonly AdminRequestHistoryViewModel _viewModel;

    public event Action<AdminRequestHistoryItemDto>? RequestSelected;

    public AdminRequestHistoryView(AdminRequestHistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void HistoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedItem is { } item)
            RequestSelected?.Invoke(item);
    }
}
