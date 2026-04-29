using System;
using System.Windows;
using System.Windows.Controls;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminSettingsView : UserControl
{
    private readonly AdminSettingsViewModel _viewModel;

    public AdminSettingsView(AdminSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.SaveAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save admin settings:\n{ex.Message}",
                "Admin Settings Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
