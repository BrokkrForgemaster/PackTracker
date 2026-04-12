using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class DashboardView : UserControl
{
    private readonly DashboardViewModel _vm;
    private ChatWindowViewModel? _dragging;
    private Point _dragOffset;

    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;
    }

    private void ChatHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ChatWindowViewModel vm)
            return;

        _dragging = vm;
        var pos = e.GetPosition(ChatWorkspaceCanvas);
        _dragOffset = new Point(pos.X - vm.Left, pos.Y - vm.Top);

        fe.CaptureMouse();
        vm.PopFrontCommand.Execute(null);
        e.Handled = true;
    }

    private void ChatHeader_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(ChatWorkspaceCanvas);
        _dragging.Left = Math.Max(0, pos.X - _dragOffset.X);
        _dragging.Top = Math.Max(0, pos.Y - _dragOffset.Y);
        e.Handled = true;
    }

    private void ChatHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging == null) return;

        if (sender is FrameworkElement fe)
            fe.ReleaseMouseCapture();

        _dragging = null;
        e.Handled = true;
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
        {
            mw.ShowHelp("Operations Dashboard & Chat", "chat-dashboard");
        }
    }
}
