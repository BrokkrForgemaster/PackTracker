using System;
using System.Windows;
using System.Windows.Controls;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminMembersView : UserControl
{
    private readonly AdminMembersViewModel _viewModel;

    public AdminMembersView(AdminMembersViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void AssignRole_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.AssignSelectedRoleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Assign Admin Role Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RevokeRole_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.RevokeSelectedRoleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Revoke Admin Role Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
