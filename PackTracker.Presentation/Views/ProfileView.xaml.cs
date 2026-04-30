using System;
using System.Windows;
using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class ProfileView : UserControl
{
    private readonly ProfileViewModel _viewModel;

    public ProfileView(ProfileViewModel viewModel)
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
            MessageBox.Show(ex.Message, "Save Profile Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
