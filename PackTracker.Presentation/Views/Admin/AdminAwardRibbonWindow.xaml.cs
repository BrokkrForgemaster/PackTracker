using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminAwardRibbonWindow : Window
{
    private readonly AdminMedalsViewModel _viewModel;
    private readonly IReadOnlyList<RibbonEntry> _allRibbons;
    private readonly List<MemberItem> _allMembers;
    private MemberItem? _selectedMember;
    private RibbonEntry? _selectedRibbon;

    private sealed record MemberItem(Guid ProfileId, string DisplayLabel, string SubLabel);

    public AdminAwardRibbonWindow(
        IReadOnlyList<AdminMemberListItemDto> members,
        IReadOnlyList<RibbonEntry> ribbons,
        AdminMedalsViewModel viewModel,
        Window owner)
    {
        InitializeComponent();
        Owner = owner;
        _viewModel = viewModel;
        _allRibbons = ribbons;
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
        RibbonPickerList.ItemsSource = _allRibbons;
    }

    private void ApplyScreenSize()
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Clamp(workArea.Width * 0.60, 620, 1000);
        Height = Math.Clamp(workArea.Height * 0.70, 480, 860);
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

    private void RibbonPicker_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not RibbonEntry ribbon) return;
        _selectedRibbon = ribbon;
        SelectedRibbonLabel.Text = ribbon.Name;
        ValidationMessage.Visibility = Visibility.Collapsed;
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMember is null)
        {
            ValidationMessage.Text = "Please select a member.";
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }
        if (_selectedRibbon is null)
        {
            ValidationMessage.Text = "Please select a ribbon.";
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }

        var request = new AwardRibbonRequestDto(
            RibbonName: _selectedRibbon.Name,
            RibbonDescription: _selectedRibbon.Description,
            RibbonImagePath: _selectedRibbon.RawImagePath,
            ProfileId: _selectedMember.ProfileId,
            RecipientName: _selectedMember.DisplayLabel,
            Citation: string.IsNullOrWhiteSpace(CitationBox.Text) ? null : CitationBox.Text.Trim());

        try
        {
            var result = await _viewModel.AwardRibbonAsync(request);

            if (result is null)
            {
                ValidationMessage.Text = "Award failed. Please try again.";
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            if (result.AlreadyAwarded)
            {
                ValidationMessage.Text = $"This ribbon has already been awarded to {result.RecipientName}.";
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            Close();
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
