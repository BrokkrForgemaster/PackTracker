using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminMedalsView : UserControl
{
    private readonly AdminMedalsViewModel _viewModel;
    private readonly AdminNominationsViewModel _nominationsVm;

    public AdminMedalsView(AdminMedalsViewModel viewModel, AdminNominationsViewModel nominationsVm)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _nominationsVm = nominationsVm;

        Loaded += async (_, _) =>
        {
            await _viewModel.LoadAsync();
            await _nominationsVm.LoadAsync();
            RefreshNominations();
        };

        _nominationsVm.PendingNominations.CollectionChanged += (_, _) => RefreshNominations();
        _nominationsVm.HistoryNominations.CollectionChanged += (_, _) => RefreshNominations();
    }

    private void RefreshNominations()
    {
        PendingList.ItemsSource = _nominationsVm.PendingNominations;
        HistoryGrid.ItemsSource = _nominationsVm.HistoryNominations;
        PendingEmptyState.Visibility = _nominationsVm.PendingNominations.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PendingTab_Click(object sender, RoutedEventArgs e)
    {
        PendingPanel.Visibility = Visibility.Visible;
        HistoryGrid.Visibility = Visibility.Collapsed;
    }

    private void HistoryTab_Click(object sender, RoutedEventArgs e)
    {
        PendingPanel.Visibility = Visibility.Collapsed;
        HistoryGrid.Visibility = Visibility.Visible;
    }

    private void NominateButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AdminNominateWindow(
            _nominationsVm.Members,
            _nominationsVm.Medals,
            _nominationsVm,
            Window.GetWindow(this)!);
        window.ShowDialog();
    }

    private void NominationCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not MedalNominationDto nomination) return;
        new AdminNominationReviewWindow(nomination, _nominationsVm, Window.GetWindow(this)!).ShowDialog();
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
                _viewModel.ImportJson = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load Medal File Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadRibbons_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PrepareRibbonsForImport();
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

    private void MedalCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not AdminMedalDefinitionDto medal) return;
        new MedalDetailWindow(medal, Window.GetWindow(this)!).ShowDialog();
    }

    private void RibbonCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not RibbonEntry ribbon) return;
        new MedalDetailWindow(ribbon, Window.GetWindow(this)!).ShowDialog();
    }

    private void AwardRibbonButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AdminAwardRibbonWindow(
            _nominationsVm.Members,
            _viewModel.Ribbons.ToList(),
            _viewModel,
            Window.GetWindow(this)!);
        window.ShowDialog();
    }
}
