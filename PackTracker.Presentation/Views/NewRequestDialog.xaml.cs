using System;
using System.Windows;
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

        // Subscribe to success event to close dialog
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

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe to prevent memory leaks
        _viewModel.RequestSubmitted -= OnRequestSubmitted;
        base.OnClosed(e);
    }
}