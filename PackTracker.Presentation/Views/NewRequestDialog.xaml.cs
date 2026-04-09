using System;
using System.Windows;
using System.Windows.Input;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class NewRequestDialog : Window
{
    private readonly NewRequestViewModel _viewModel;

    public NewRequestDialog(NewRequestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestSubmitted += OnRequestSubmitted;
    }

    private void OnRequestSubmitted(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.RequestSubmitted -= OnRequestSubmitted;
        base.OnClosed(e);
    }
}