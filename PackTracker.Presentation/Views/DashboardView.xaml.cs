using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class DashboardView : UserControl
{
    private readonly DashboardViewModel _vm;
    public DashboardViewModel ViewModel => _vm;

    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;
    }

    private void Channel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is AvailableChannelViewModel channel)
            _vm.SelectChannelCommand.Execute(channel);
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
        {
            mw.ShowHelp("Operations Dashboard & Chat", "chat-dashboard");
        }
    }

    private async void ActiveRequest_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ActiveRequestDto request)
            return;

        if (Window.GetWindow(this) is MainWindow mw)
            await mw.NavigateToActiveRequestAsync(request);
    }
}
