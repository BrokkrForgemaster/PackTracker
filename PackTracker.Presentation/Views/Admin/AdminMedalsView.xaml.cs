using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminMedalsView : UserControl
{
    private readonly AdminMedalsViewModel _viewModel;

    public AdminMedalsView(AdminMedalsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void LoadFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select medals JSON export",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.ImportJson = File.ReadAllText(dialog.FileName);
                _viewModel.StatusMessage = $"Loaded import file: {Path.GetFileName(dialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load Medal File Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ImportAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import Medals Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
