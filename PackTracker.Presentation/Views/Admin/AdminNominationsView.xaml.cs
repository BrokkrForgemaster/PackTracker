using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminNominationsView : UserControl
{
    private readonly AdminNominationsViewModel _viewModel;

    public AdminNominationsView(AdminNominationsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
        _viewModel.PendingNominations.CollectionChanged += (_, _) => UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        PendingEmptyState.Visibility = _viewModel.PendingNominations.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PendingTab_Click(object sender, RoutedEventArgs e)
    {
        PendingPanel.Visibility = Visibility.Visible;
        HistoryPanel.Visibility = Visibility.Collapsed;
    }

    private void HistoryTab_Click(object sender, RoutedEventArgs e)
    {
        PendingPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Visible;
    }

    private void NominateButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AdminNominateWindow(
            _viewModel.Members,
            _viewModel.Medals,
            _viewModel,
            Window.GetWindow(this)!);
        window.ShowDialog();
    }

    private void NominationCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not MedalNominationDto nomination) return;
        var window = new AdminNominationReviewWindow(nomination, _viewModel, Window.GetWindow(this)!);
        window.ShowDialog();
    }
}
