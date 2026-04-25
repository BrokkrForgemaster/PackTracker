using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
        // Dismiss buttons inside cards must not trigger navigation
        if (e.OriginalSource is DependencyObject src && IsInsideButtonBase(src))
            return;

        if (sender is not FrameworkElement fe || fe.DataContext is not ActiveRequestItemViewModel itemVm)
            return;

        if (Window.GetWindow(this) is MainWindow mw)
            await mw.NavigateToActiveRequestAsync(itemVm.Dto);
    }

    private static bool IsInsideButtonBase(DependencyObject element)
    {
        var current = element;
        while (current != null)
        {
            if (current is ButtonBase)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void ChatInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Return &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                var caret = tb.CaretIndex;
                tb.Text = tb.Text.Insert(caret, "\n");
                tb.CaretIndex = caret + 1;
                tb.AcceptsReturn = false;
                e.Handled = true;
            }
        }
    }
}
