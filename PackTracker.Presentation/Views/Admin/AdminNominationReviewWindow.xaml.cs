using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminNominationReviewWindow : Window
{
    private readonly MedalNominationDto _nomination;
    private readonly AdminNominationsViewModel _viewModel;
    private readonly bool _isPending;

    public AdminNominationReviewWindow(
        MedalNominationDto nomination,
        AdminNominationsViewModel viewModel,
        Window owner)
    {
        InitializeComponent();
        Owner = owner;
        _nomination = nomination;
        _viewModel = viewModel;
        _isPending = nomination.Status == "Pending";

        ApplyScreenSize();
        Populate();
    }

    private void ApplyScreenSize()
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Clamp(workArea.Width * 0.50, 520, 900);
        Height = Math.Clamp(workArea.Height * 0.65, 440, 780);
    }

    private void Populate()
    {
        MedalNameText.Text = _nomination.MedalName;
        NomineeNameText.Text = _nomination.NomineeName;
        NominatorNameText.Text = _nomination.NominatorName;
        CitationText.Text = _nomination.Citation;

        if (!string.IsNullOrEmpty(_nomination.MedalImagePath))
        {
            try
            {
                var normalized = _nomination.MedalImagePath.Replace('\\', '/');
                while (normalized.StartsWith("../") || normalized.StartsWith("./"))
                    normalized = normalized[(normalized.IndexOf('/') + 1)..];
                MedalImage.Source = new BitmapImage(new Uri($"pack://application:,,,/{normalized}"));
            }
            catch { /* image stays null */ }
        }

        if (!_isPending)
        {
            ActionButtons.Visibility = Visibility.Collapsed;
            NotesInputPanel.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(_nomination.ReviewNotes))
            {
                ReviewNotesText.Text = _nomination.ReviewNotes;
                ReviewNotesPanel.Visibility = Visibility.Visible;
            }
        }
    }

    private async void Approve_Click(object sender, RoutedEventArgs e)
    {
        ApproveButton.IsEnabled = false;
        DenyButton.IsEnabled = false;
        var success = await _viewModel.ApproveAsync(_nomination.Id, ReviewNotesInput.Text.Trim());
        if (success)
            Close();
        else
        {
            ApproveButton.IsEnabled = true;
            DenyButton.IsEnabled = true;
            MessageBox.Show("Failed to approve nomination.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Deny_Click(object sender, RoutedEventArgs e)
    {
        ApproveButton.IsEnabled = false;
        DenyButton.IsEnabled = false;
        var success = await _viewModel.DenyAsync(_nomination.Id, ReviewNotesInput.Text.Trim());
        if (success)
            Close();
        else
        {
            ApproveButton.IsEnabled = true;
            DenyButton.IsEnabled = true;
            MessageBox.Show("Failed to deny nomination.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
