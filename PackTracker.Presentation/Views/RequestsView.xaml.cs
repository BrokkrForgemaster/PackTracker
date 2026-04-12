using System.Windows;
using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

/// <summary name="RequestsView">
/// Interaction logic for RequestsView.xaml
/// </summary>
public partial class RequestsView : UserControl
{
    private readonly RequestsViewModel _viewModel;

    public RequestsView(RequestsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
        {
            mw.ShowHelp("Assistance Hub", "request-hub");
        }
    }
}