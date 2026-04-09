using System.Windows;
using System.Windows.Input;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class CraftingRequestFormDialog : Window
{
    private readonly CraftingRequestFormViewModel _viewModel;

    public CraftingRequestFormDialog(CraftingRequestFormViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.Submitted += (_, _) => { DialogResult = true; Close(); };
        _viewModel.Cancelled += (_, _) => { DialogResult = false; Close(); };
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
