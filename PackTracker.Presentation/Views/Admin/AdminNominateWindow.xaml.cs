using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminNominateWindow : Window
{
    private readonly AdminNominationsViewModel _viewModel;
    private readonly List<MemberItem> _allMembers;
    private MemberItem? _selectedMember;
    private AdminMedalDefinitionDto? _selectedMedal;

    private sealed record MemberItem(Guid ProfileId, string DisplayLabel, string SubLabel);

    public AdminNominateWindow(
        IReadOnlyList<AdminMemberListItemDto> members,
        IReadOnlyList<AdminMedalDefinitionDto> medals,
        AdminNominationsViewModel viewModel,
        Window owner)
    {
        InitializeComponent();
        Owner = owner;
        _viewModel = viewModel;
        _allMembers = members
            .Select(m => new MemberItem(
                m.ProfileId,
                m.DisplayName ?? m.Username,
                string.Join(" · ", new[] { m.DiscordRank, m.DiscordDivision }
                    .Where(s => !string.IsNullOrEmpty(s)))))
            .OrderBy(m => m.DisplayLabel)
            .ToList();

        ApplyScreenSize();
        RefreshMemberList(string.Empty);
        MedalPickerList.ItemsSource = medals;
    }

    private void ApplyScreenSize()
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Clamp(workArea.Width * 0.72, 700, 1200);
        Height = Math.Clamp(workArea.Height * 0.75, 500, 900);
    }

    private void RefreshMemberList(string filter)
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allMembers
            : _allMembers
                .Where(m => m.DisplayLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                             m.SubLabel.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        MemberListBox.ItemsSource = filtered;
    }

    private void MemberSearch_TextChanged(object sender, TextChangedEventArgs e)
        => RefreshMemberList(MemberSearch.Text);

    private void MemberListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedMember = MemberListBox.SelectedItem as MemberItem;
        SelectedMemberLabel.Text = _selectedMember?.DisplayLabel ?? "None selected";
        ValidationMessage.Visibility = Visibility.Collapsed;
    }

    private void MedalPicker_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not AdminMedalDefinitionDto medal) return;
        _selectedMedal = medal;
        SelectedMedalLabel.Text = medal.Name;
        ValidationMessage.Visibility = Visibility.Collapsed;
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMember is null)
        {
            ValidationMessage.Text = "Please select a member to nominate.";
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }
        if (_selectedMedal is null)
        {
            ValidationMessage.Text = "Please select a medal.";
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }
        if (string.IsNullOrWhiteSpace(CitationBox.Text))
        {
            ValidationMessage.Text = "Please enter a citation.";
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }

        var request = new SubmitMedalNominationRequestDto(
            _selectedMedal.Id,
            _selectedMember.ProfileId,
            _selectedMember.DisplayLabel,
            CitationBox.Text.Trim());

        try
        {
            var result = await _viewModel.SubmitAsync(request);
            if (result is not null)
                Close();
            else
            {
                ValidationMessage.Text = "Submission failed. Please try again.";
                ValidationMessage.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ValidationMessage.Text = ex.Message;
            ValidationMessage.Visibility = Visibility.Visible;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
